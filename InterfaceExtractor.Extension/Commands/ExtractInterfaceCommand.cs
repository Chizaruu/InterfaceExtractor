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
        private readonly DTE2 dte;
        private IVsOutputWindowPane outputPane;

        private ExtractInterfaceCommand(AsyncPackage package, OleMenuCommandService commandService, DTE2 dte)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.dte = dte ?? throw new ArgumentNullException(nameof(dte));
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

            // Get services asynchronously
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;

            Instance = new ExtractInterfaceCommand(package, commandService, dte);

            // Initialize output pane
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

            // Pattern matching (C# 7.3 compatible)
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
            // Use JoinableTaskFactory.RunAsync for proper async execution from sync context
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LogMessage($"Critical error: {ex.Message}");
                    LogMessage($"Stack trace: {ex.StackTrace}");
                    ShowMessage($"Error: {ex.Message}\n\nCheck the Output Window for details.");
                }
            }).FileAndForget("InterfaceExtractor/Execute");
        }

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            LogMessage("Starting interface extraction...");

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
                .Where(path => Path.GetExtension(path).Equals(Constants.CSharpExtension, StringComparison.OrdinalIgnoreCase))
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

            // Track overwrite preference across all files
            OverwriteChoice overwriteChoice = OverwriteChoice.Ask;

            foreach (var filePath in selectedFiles)
            {
                LogMessage($"Analyzing: {Path.GetFileName(filePath)}");

                try
                {
                    // Analyze the class(es)
                    var classInfos = await extractorService.AnalyzeClassesAsync(filePath);

                    if (!classInfos.Any())
                    {
                        LogMessage($"  No public classes with members found in {Path.GetFileName(filePath)}");
                        skippedCount++;
                        continue;
                    }

                    // If multiple classes, let user choose or process all
                    foreach (var classInfo in classInfos)
                    {
                        LogMessage($"  Found class: {classInfo.ClassName} with {classInfo.Members.Count} public member(s)");

                        if (!classInfo.Members.Any())
                        {
                            LogMessage($"  No public members found in class {classInfo.ClassName}");
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
                        var dialogResult = dialog.ShowDialog();

                        if (dialogResult != true)
                        {
                            LogMessage($"  User cancelled extraction for {classInfo.ClassName}");
                            skippedCount++;
                            continue;
                        }

                        // Validate interface name
                        if (!IsValidInterfaceName(dialog.InterfaceName, out string validationError))
                        {
                            ShowMessage($"Invalid interface name: {validationError}");
                            failCount++;
                            continue;
                        }

                        // Get selected members
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

                        // Generate interface code
                        var interfaceCode = Services.InterfaceExtractorService.GenerateInterface(
                            dialog.InterfaceName,
                            classInfo,
                            selectedMembers);

                        // Save interface file
                        var interfacesFolder = Path.Combine(Path.GetDirectoryName(filePath), Constants.InterfacesFolderName);
                        Directory.CreateDirectory(interfacesFolder);

                        var interfaceFilePath = Path.Combine(interfacesFolder, $"{dialog.InterfaceName}{Constants.CSharpExtension}");

                        // Check if file exists
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

                        File.WriteAllText(interfaceFilePath, interfaceCode);
                        LogMessage($"  Created: {interfaceFilePath}");

                        // Update the original class to implement the interface
                        try
                        {
                            var originalCode = File.ReadAllText(filePath);
                            var updatedCode = Services.InterfaceExtractorService.AppendInterfaceToClass(
                                originalCode,
                                classInfo.ClassName,
                                dialog.InterfaceName,
                                $"{classInfo.Namespace}{Constants.InterfacesNamespaceSuffix}");

                            if (updatedCode != originalCode)
                            {
                                File.WriteAllText(filePath, updatedCode);
                                LogMessage($"  Updated class to implement {dialog.InterfaceName}");
                            }
                            else
                            {
                                LogMessage($"  Class already implements {dialog.InterfaceName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"  Warning: Could not update class to implement interface: {ex.Message}");
                            // Continue - interface was still created successfully
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
                                        return pi.Name == Constants.InterfacesFolderName;
                                    }) ?? projectItems.AddFolder(Constants.InterfacesFolderName);

                                // Check if already in project
                                var existingItem = interfacesFolderItem?.ProjectItems.Cast<ProjectItem>()
                                    .FirstOrDefault(pi =>
                                    {
                                        ThreadHelper.ThrowIfNotOnUIThread();
                                        return pi.Name == $"{dialog.InterfaceName}{Constants.CSharpExtension}";
                                    });

                                if (existingItem == null)
                                {
                                    interfacesFolderItem?.ProjectItems.AddFromFile(interfaceFilePath);
                                    LogMessage($"  Added to project");
                                }
                                else
                                {
                                    LogMessage($"  File already in project");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"  Warning: Could not add file to project: {ex.Message}");
                                // File created successfully, just couldn't add to project
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

            // Check if valid C# identifier
            if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name))
            {
                error = "Interface name is not a valid C# identifier.";
                return false;
            }

            // Check if it's a reserved keyword
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

    /// <summary>
    /// Represents the user's choice for overwriting files (internal tracking)
    /// </summary>
    internal enum OverwriteChoice
    {
        Ask,
        YesToAll,
        NoToAll
    }

    /// <summary>
    /// Extension methods for JoinableTask fire-and-forget operations
    /// </summary>
    internal static class JoinableTaskExtensions
    {
        /// <summary>
        /// Allows fire-and-forget for JoinableTask while ensuring proper exception handling
        /// </summary>
        public static void FileAndForget(this Microsoft.VisualStudio.Threading.JoinableTask joinableTask, string context)
        {
            // JoinableTask already handles the async operation properly
            // Just need to observe it to prevent unobserved task exceptions
            _ = joinableTask.Task.ContinueWith(
                t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        // Log to activity log
                        ActivityLog.LogError(context, $"Unhandled exception: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
                    }
                },
                System.Threading.Tasks.TaskScheduler.Default);
        }
    }
}