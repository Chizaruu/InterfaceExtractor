using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using InterfaceExtractor.Options;
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
        private readonly DTE2 dte;
        private readonly ExtractorOptions options;
        private IVsOutputWindowPane outputPane;

        private ExtractInterfaceCommand(AsyncPackage package, OleMenuCommandService commandService, DTE2 dte)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.dte = dte ?? throw new ArgumentNullException(nameof(dte));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            options = OptionsProvider.GetOptions(package);
            extractorService = new Services.InterfaceExtractorService(options);

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
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;

            Instance = new ExtractInterfaceCommand(package, commandService, dte);

            await Instance.InitializeOutputPaneAsync();
        }

        private async Task InitializeOutputPaneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (await package.GetServiceAsync(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow)
            {
                var customGuid = new Guid("B8A0C8E1-2F3E-4D5A-9C8B-7E6F5A4D3C2B");
                outWindow.CreatePane(ref customGuid, Constants.OutputPaneName, 1, 1);
                outWindow.GetPane(ref customGuid, out outputPane);
            }
        }

        private void LogMessage(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            outputPane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            outputPane?.Activate();
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(sender is OleMenuCommand command)) return;

            command.Visible = false;
            command.Enabled = false;

            if (dte?.SelectedItems == null) return;

            foreach (SelectedItem item in dte.SelectedItems)
            {
                if (item.ProjectItem?.FileNames[1] != null)
                {
                    var fileName = item.ProjectItem.FileNames[1];
                    if (Path.GetExtension(fileName).Equals(Constants.CSharpExtension, StringComparison.OrdinalIgnoreCase))
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
            this.package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    await this.package.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LogMessage($"Critical error: {ex.Message}");
                    LogMessage($"Stack trace: {ex.StackTrace}");
                    ShowMessage($"Error: {ex.Message}\n\nCheck the Output Window for details.");
                }
            }).FileAndForget("InterfaceExtractor/Execute");
        }

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            LogMessage("=== Interface Extractor v1.2.0 ===");
            LogMessage("Starting interface extraction...");
            LogMessage($"Options: Folder={options.InterfacesFolderName}, Prefix={options.InterfacePrefix}");
            LogMessage($"  AutoUpdate={options.AutoUpdateClass}, IncludeOperators={options.IncludeOperatorOverloads}");
            LogMessage($"  ShowPreview={options.ShowPreviewBeforeSaving}, IncludeInternal={options.IncludeInternalMembers}");
            LogMessage($"  AnalyzePartial={options.AnalyzePartialClasses}, GenerateStubs={options.GenerateImplementationStubs}");

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
                    return new
                    {
                        Path = item.ProjectItem.FileNames[1],
                        item.ProjectItem
                    };
                })
                .Where(f => Path.GetExtension(f.Path).Equals(Constants.CSharpExtension, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!selectedFiles.Any())
            {
                ShowMessage("No C# files selected.");
                return;
            }

            LogMessage($"Processing {selectedFiles.Count} file(s)...");

            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;

            OverwriteChoice overwriteChoice = OverwriteChoice.Ask;

            foreach (var file in selectedFiles)
            {
                var filePath = file.Path;
                LogMessage($"Analyzing: {Path.GetFileName(filePath)}");

                try
                {
                    // Get project directory for partial class analysis
                    string projectDirectory = null;
                    if (file.ProjectItem?.ContainingProject != null)
                    {
                        var projectPath = file.ProjectItem.ContainingProject.FullName;
                        projectDirectory = Path.GetDirectoryName(projectPath);
                    }

                    var classInfos = await extractorService.AnalyzeClassesAsync(filePath, projectDirectory);

                    if (!classInfos.Any())
                    {
                        LogMessage($"  No public classes/records with members found in {Path.GetFileName(filePath)}");
                        skippedCount++;
                        continue;
                    }

                    foreach (var classInfo in classInfos)
                    {
                        var typeKind = classInfo.IsRecord ? "record" : "class";
                        var partialInfo = classInfo.IsPartial ? " (partial)" : "";

                        LogMessage($"  Found {typeKind}: {classInfo.ClassName}{partialInfo} with {classInfo.Members.Count} member(s)");

                        if (!classInfo.Members.Any())
                        {
                            LogMessage($"  No accessible members found in {typeKind} {classInfo.ClassName}");
                            continue;
                        }

                        var selectionItems = classInfo.Members.Select(m => new UI.MemberSelectionItem
                        {
                            DisplayText = m.Signature,
                            Signature = m.Signature,
                            MemberType = m.Type.ToString(),
                            Constraints = m.Constraints,
                            IsSelected = true
                        }).ToList();

                        var dialog = new UI.ExtractInterfaceDialog(classInfo.ClassName, selectionItems, options);
                        var dialogResult = dialog.ShowDialog();

                        if (dialogResult != true)
                        {
                            LogMessage($"  User cancelled extraction for {classInfo.ClassName}");
                            skippedCount++;
                            continue;
                        }

                        if (!IsValidInterfaceName(dialog.InterfaceName, out string validationError))
                        {
                            ShowMessage($"Invalid interface name: {validationError}");
                            failCount++;
                            continue;
                        }

                        var selectedMembers = classInfo.Members
                            .Where((m, i) => selectionItems[i].IsSelected)
                            .ToList();

                        if (!selectedMembers.Any())
                        {
                            ShowMessage("No members selected.");
                            skippedCount++;
                            continue;
                        }

                        LogMessage($"  Generating interface {dialog.InterfaceName} with {selectedMembers.Count} member(s)");

                        var interfaceCode = extractorService.GenerateInterface(
                            dialog.InterfaceName,
                            classInfo,
                            selectedMembers);

                        var interfacesFolder = Path.Combine(Path.GetDirectoryName(filePath), options.InterfacesFolderName);
                        Directory.CreateDirectory(interfacesFolder);

                        var interfaceFilePath = Path.Combine(interfacesFolder, $"{dialog.InterfaceName}{Constants.CSharpExtension}");

                        // Show preview if enabled (v1.2.0)
                        if (options.ShowPreviewBeforeSaving)
                        {
                            var previewDialog = new UI.PreviewDialog(
                                dialog.InterfaceName,
                                interfaceFilePath,
                                interfaceCode);

                            var previewResult = previewDialog.ShowDialog();

                            if (previewResult != true || !previewDialog.UserApproved)
                            {
                                LogMessage($"  User cancelled after preview");
                                skippedCount++;
                                continue;
                            }

                            LogMessage($"  User approved preview");
                        }

                        // Handle existing file
                        if (File.Exists(interfaceFilePath))
                        {
                            bool shouldOverwrite = false;

                            switch (overwriteChoice)
                            {
                                case OverwriteChoice.YesToAll:
                                    shouldOverwrite = true;
                                    LogMessage($"  Overwriting existing file (Yes to All)");
                                    break;

                                case OverwriteChoice.NoToAll:
                                    shouldOverwrite = false;
                                    LogMessage($"  Skipping existing file (No to All)");
                                    break;

                                case OverwriteChoice.Ask:
                                    var overwriteResult = ShowOverwriteConfirmation(
                                        $"{dialog.InterfaceName}{Constants.CSharpExtension}");

                                    switch (overwriteResult)
                                    {
                                        case UI.OverwriteChoice.Yes:
                                            shouldOverwrite = true;
                                            break;

                                        case UI.OverwriteChoice.YesToAll:
                                            shouldOverwrite = true;
                                            overwriteChoice = OverwriteChoice.YesToAll;
                                            LogMessage($"  Selected 'Yes to All' for remaining files");
                                            break;

                                        case UI.OverwriteChoice.No:
                                            shouldOverwrite = false;
                                            break;

                                        case UI.OverwriteChoice.NoToAll:
                                            shouldOverwrite = false;
                                            overwriteChoice = OverwriteChoice.NoToAll;
                                            LogMessage($"  Selected 'No to All' for remaining files");
                                            break;
                                    }
                                    break;
                            }

                            if (!shouldOverwrite)
                            {
                                LogMessage($"  User chose not to overwrite existing file");
                                skippedCount++;
                                continue;
                            }
                        }

                        // Write interface file
                        File.WriteAllText(interfaceFilePath, interfaceCode);
                        LogMessage($"  Created: {interfaceFilePath}");

                        // Generate implementation stub if enabled (v1.2.0)
                        if (options.GenerateImplementationStubs)
                        {
                            var stubClassName = $"{classInfo.ClassName}{options.ImplementationStubSuffix}";
                            var stubCode = extractorService.GenerateImplementationStub(
                                dialog.InterfaceName,
                                stubClassName,
                                classInfo,
                                selectedMembers);

                            var stubFilePath = Path.Combine(interfacesFolder, $"{stubClassName}{Constants.CSharpExtension}");

                            if (!File.Exists(stubFilePath) || overwriteChoice == OverwriteChoice.YesToAll)
                            {
                                File.WriteAllText(stubFilePath, stubCode);
                                LogMessage($"  Created implementation stub: {stubFilePath}");
                            }
                            else
                            {
                                LogMessage($"  Skipped implementation stub (file exists): {stubClassName}");
                            }
                        }

                        // Update class to implement interface
                        if (options.AutoUpdateClass)
                        {
                            try
                            {
                                var originalCode = File.ReadAllText(filePath);
                                var updatedCode = extractorService.AppendInterfaceToClass(
                                    originalCode,
                                    classInfo.ClassName,
                                    dialog.InterfaceName,
                                    $"{classInfo.Namespace}{options.InterfacesNamespaceSuffix}");

                                if (updatedCode != originalCode)
                                {
                                    File.WriteAllText(filePath, updatedCode);
                                    LogMessage($"  Updated {typeKind} to implement {dialog.InterfaceName}");
                                }
                                else
                                {
                                    LogMessage($"  {typeKind.Substring(0, 1).ToUpper()}{typeKind.Substring(1)} already implements {dialog.InterfaceName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"  Warning: Could not update {typeKind} to implement interface: {ex.Message}");
                            }
                        }
                        else
                        {
                            LogMessage($"  Skipped {typeKind} update (disabled in options)");
                        }

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
                                        return pi.Name == options.InterfacesFolderName;
                                    }) ?? projectItems.AddFolder(options.InterfacesFolderName);

                                // Add interface file
                                var existingInterfaceItem = interfacesFolderItem?.ProjectItems.Cast<ProjectItem>()
                                    .FirstOrDefault(pi =>
                                    {
                                        ThreadHelper.ThrowIfNotOnUIThread();
                                        return pi.Name == $"{dialog.InterfaceName}{Constants.CSharpExtension}";
                                    });

                                if (existingInterfaceItem == null)
                                {
                                    interfacesFolderItem?.ProjectItems.AddFromFile(interfaceFilePath);
                                    LogMessage($"  Added interface to project");
                                }
                                else
                                {
                                    LogMessage($"  Interface file already in project");
                                }

                                // Add implementation stub file if generated
                                if (options.GenerateImplementationStubs)
                                {
                                    var stubClassName = $"{classInfo.ClassName}{options.ImplementationStubSuffix}";
                                    var stubFilePath = Path.Combine(interfacesFolder, $"{stubClassName}{Constants.CSharpExtension}");

                                    if (File.Exists(stubFilePath))
                                    {
                                        var existingStubItem = interfacesFolderItem?.ProjectItems.Cast<ProjectItem>()
                                            .FirstOrDefault(pi =>
                                            {
                                                ThreadHelper.ThrowIfNotOnUIThread();
                                                return pi.Name == $"{stubClassName}{Constants.CSharpExtension}";
                                            });

                                        if (existingStubItem == null)
                                        {
                                            interfacesFolderItem?.ProjectItems.AddFromFile(stubFilePath);
                                            LogMessage($"  Added implementation stub to project");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"  Warning: Could not add file(s) to project: {ex.Message}");
                            }
                        }

                        successCount++;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    failCount++;
                    LogMessage($"  Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                    ShowMessage($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    failCount++;
                    LogMessage($"  Unexpected error processing {Path.GetFileName(filePath)}: {ex.Message}");
                    LogMessage($"  Stack trace: {ex.StackTrace}");
                    ShowMessage($"Unexpected error processing {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            var summary = $"Interface extraction complete!\n\n" +
                         $"Succeeded: {successCount}\n" +
                         $"Failed: {failCount}\n" +
                         $"Skipped: {skippedCount}";

            LogMessage("=== Extraction Complete ===");
            LogMessage(summary.Replace("\n", " "));

            if (successCount > 0 || failCount > 0)
            {
                ShowMessage(summary);
            }
        }

        private static bool IsValidInterfaceName(string name, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Interface name cannot be empty.";
                return false;
            }

            if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name))
            {
                error = "Interface name is not a valid C# identifier.";
                return false;
            }

            if (Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)
            {
                error = "Interface name cannot be a C# keyword.";
                return false;
            }

            return true;
        }

        private void ShowMessage(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                Constants.ExtensionName,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private static UI.OverwriteChoice ShowOverwriteConfirmation(string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dialog = new UI.OverwriteDialog(fileName);
            dialog.ShowDialog();
            return dialog.Choice;
        }
    }

    internal enum OverwriteChoice
    {
        Ask,
        YesToAll,
        NoToAll
    }

    internal static class JoinableTaskExtensions
    {
        public static void FileAndForget(this Microsoft.VisualStudio.Threading.JoinableTask joinableTask, string context)
        {
            _ = joinableTask.Task.ContinueWith(
                t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        ActivityLog.LogError(context, $"Unhandled exception: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
                    }
                },
                System.Threading.Tasks.TaskScheduler.Default);
        }
    }
}