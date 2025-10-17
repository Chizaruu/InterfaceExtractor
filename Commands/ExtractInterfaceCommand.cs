using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace InterfaceExtractor.Commands
{
    internal sealed class ExtractInterfaceCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("a4b5c6d7-e8f9-4a5b-9c3d-2e1f0a5b6c7d");

        private readonly AsyncPackage package;
        private readonly Services.InterfaceExtractorService extractorService;

        private ExtractInterfaceCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            extractorService = new Services.InterfaceExtractorService();

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        public static ExtractInterfaceCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ExtractInterfaceCommand(package, commandService);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(sender is OleMenuCommand command)) return;

            command.Visible = false;
            command.Enabled = false;

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte?.SelectedItems == null) return;

            foreach (SelectedItem item in dte.SelectedItems)
            {
                if (item.ProjectItem?.FileNames[1] != null)
                {
                    var fileName = item.ProjectItem.FileNames[1];
                    if (Path.GetExtension(fileName).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        command.Visible = true;
                        command.Enabled = true;
                        return;
                    }
                }
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () => await ExecuteAsync());
            }
            catch (Exception ex)
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ShowMessage($"Error: {ex.Message}\n\nStack: {ex.StackTrace}");
                });
            }
        }

        private async Task ExecuteAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                if (dte?.SelectedItems == null)
                {
                    ShowMessage("No files selected.");
                    return;
                }

                var selectedFiles = dte.SelectedItems.Cast<SelectedItem>()
                    .Where(item =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        return item.ProjectItem?.FileNames[1] != null;
                    })
                    .Select(item =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        return item.ProjectItem.FileNames[1];
                    })
                    .Where(path => Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!selectedFiles.Any())
                {
                    ShowMessage("No C# files selected.");
                    return;
                }

                int successCount = 0;
                int failCount = 0;

                foreach (var filePath in selectedFiles)
                {
                    try
                    {
                        // Analyze the class
                        var classInfo = await extractorService.AnalyzeClassAsync(filePath);

                        if (classInfo == null)
                        {
                            ShowMessage($"No public class found in {Path.GetFileName(filePath)}");
                            failCount++;
                            continue;
                        }

                        if (!classInfo.Members.Any())
                        {
                            ShowMessage($"No public members found in class {classInfo.ClassName}");
                            failCount++;
                            continue;
                        }

                        // Convert to selection items
                        var selectionItems = classInfo.Members.Select(m => new UI.MemberSelectionItem
                        {
                            DisplayText = m.Signature,
                            Signature = m.Signature,
                            MemberType = m.Type.ToString(),
                            Constraints = m.Constraints,
                            IsSelected = true
                        }).ToList();

                        // Show dialog
                        var dialog = new UI.ExtractInterfaceDialog(classInfo.ClassName, selectionItems);
                        var result = dialog.ShowDialog();

                        if (result != true)
                        {
                            // User cancelled
                            continue;
                        }

                        // Get selected members
                        var selectedMembers = classInfo.Members
                            .Where((m, i) => selectionItems[i].IsSelected)
                            .ToList();

                        if (!selectedMembers.Any())
                        {
                            ShowMessage("No members selected.");
                            continue;
                        }

                        // Generate interface code
                        var interfaceCode = Services.InterfaceExtractorService.GenerateInterface(
                            dialog.InterfaceName,
                            classInfo,
                            selectedMembers);

                        // Save interface file
                        var interfacesFolder = Path.Combine(Path.GetDirectoryName(filePath), "Interfaces");
                        Directory.CreateDirectory(interfacesFolder);

                        var interfaceFilePath = Path.Combine(interfacesFolder, $"{dialog.InterfaceName}.cs");
                        File.WriteAllText(interfaceFilePath, interfaceCode);

                        // Add to project
                        var projectItem = dte.Solution.FindProjectItem(filePath);
                        if (projectItem?.ContainingProject != null)
                        {
                            try
                            {
                                var projectItems = projectItem.ContainingProject.ProjectItems;
                                var interfacesFolderItem = projectItems.Cast<ProjectItem>()
                                    .FirstOrDefault(pi =>
                                    {
                                        ThreadHelper.ThrowIfNotOnUIThread();
                                        return pi.Name == "Interfaces";
                                    }) ?? projectItems.AddFolder("Interfaces");
                                interfacesFolderItem?.ProjectItems.AddFromFile(interfaceFilePath);
                            }
                            catch
                            {
                                // File created but couldn't add to project - that's ok
                            }
                        }

                        successCount++;
                    }
                    catch (InvalidOperationException ex)
                    {
                        failCount++;
                        ShowMessage($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }

                if (successCount > 0 || failCount > 0)
                {
                    ShowMessage($"Interface extraction complete!\n\nSucceeded: {successCount}\nFailed: {failCount}");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error: {ex.Message}");
            }
        }

        private void ShowMessage(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                "Interface Extractor",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}