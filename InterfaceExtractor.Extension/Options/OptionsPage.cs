using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace InterfaceExtractor.Options
{
    /// <summary>
    /// Options page for Interface Extractor settings (Visual Studio UI)
    /// </summary>
    [ComVisible(true)]
    [Guid("A7B3C2D1-E4F5-6789-ABCD-EF0123456789")]
    public class GeneralOptionsPage : DialogPage
    {
        private readonly ExtractorOptions _options = new ExtractorOptions();

        [Category("General")]
        [DisplayName("Interface Folder Name")]
        [Description("The name of the folder where interface files will be created.")]
        [DefaultValue("Interfaces")]
        public string InterfacesFolderName
        {
            get => _options.InterfacesFolderName;
            set => _options.InterfacesFolderName = value;
        }

        [Category("General")]
        [DisplayName("Interface Prefix")]
        [Description("The prefix to use when suggesting interface names (typically 'I').")]
        [DefaultValue("I")]
        public string InterfacePrefix
        {
            get => _options.InterfacePrefix;
            set => _options.InterfacePrefix = value;
        }

        [Category("General")]
        [DisplayName("Namespace Suffix")]
        [Description("The suffix to append to the original namespace for interface files.")]
        [DefaultValue(".Interfaces")]
        public string InterfacesNamespaceSuffix
        {
            get => _options.InterfacesNamespaceSuffix;
            set => _options.InterfacesNamespaceSuffix = value;
        }

        [Category("Behavior")]
        [DisplayName("Automatically Update Class")]
        [Description("Automatically add the interface to the class declaration after generation.")]
        [DefaultValue(true)]
        public bool AutoUpdateClass
        {
            get => _options.AutoUpdateClass;
            set => _options.AutoUpdateClass = value;
        }

        [Category("Behavior")]
        [DisplayName("Add Using Directive")]
        [Description("Automatically add using directive for the interface namespace when updating the class.")]
        [DefaultValue(true)]
        public bool AddUsingDirective
        {
            get => _options.AddUsingDirective;
            set => _options.AddUsingDirective = value;
        }

        [Category("Behavior")]
        [DisplayName("Warn If No 'I' Prefix")]
        [Description("Show a warning dialog if the interface name doesn't start with the configured prefix.")]
        [DefaultValue(true)]
        public bool WarnIfNoIPrefix
        {
            get => _options.WarnIfNoIPrefix;
            set => _options.WarnIfNoIPrefix = value;
        }

        [Category("Behavior")]
        [DisplayName("Include Operator Overloads")]
        [Description("Include operator overloads (==, !=, +, -, etc.) when extracting interfaces.")]
        [DefaultValue(false)]
        public bool IncludeOperatorOverloads
        {
            get => _options.IncludeOperatorOverloads;
            set => _options.IncludeOperatorOverloads = value;
        }

        [Category("Behavior")]
        [DisplayName("Show Preview Before Saving")]
        [Description("Display a preview of the generated interface before saving to disk.")]
        [DefaultValue(true)]
        public bool ShowPreviewBeforeSaving
        {
            get => _options.ShowPreviewBeforeSaving;
            set => _options.ShowPreviewBeforeSaving = value;
        }

        [Category("Behavior")]
        [DisplayName("Include Internal Members")]
        [Description("Include internal members in addition to public members when extracting interfaces.")]
        [DefaultValue(false)]
        public bool IncludeInternalMembers
        {
            get => _options.IncludeInternalMembers;
            set => _options.IncludeInternalMembers = value;
        }

        [Category("Behavior")]
        [DisplayName("Analyze Partial Classes")]
        [Description("When extracting from partial classes, analyze all partial files to include all members.")]
        [DefaultValue(true)]
        public bool AnalyzePartialClasses
        {
            get => _options.AnalyzePartialClasses;
            set => _options.AnalyzePartialClasses = value;
        }

        [Category("Code Generation")]
        [DisplayName("Generate Implementation Stubs")]
        [Description("Generate a file with empty implementation stubs for the interface members.")]
        [DefaultValue(false)]
        public bool GenerateImplementationStubs
        {
            get => _options.GenerateImplementationStubs;
            set => _options.GenerateImplementationStubs = value;
        }

        [Category("Code Generation")]
        [DisplayName("Implementation Stub Suffix")]
        [Description("Suffix to append to implementation stub class names (e.g., 'Implementation', 'Service').")]
        [DefaultValue("Implementation")]
        public string ImplementationStubSuffix
        {
            get => _options.ImplementationStubSuffix;
            set => _options.ImplementationStubSuffix = value;
        }

        [Category("Templates")]
        [DisplayName("Include File Header")]
        [Description("Include a header comment at the top of generated interface files.")]
        [DefaultValue(false)]
        public bool IncludeFileHeader
        {
            get => _options.IncludeFileHeader;
            set => _options.IncludeFileHeader = value;
        }

        [Category("Templates")]
        [DisplayName("File Header Template")]
        [Description("The template for file headers. Use {FileName}, {Date}, {Time} placeholders.")]
        [DefaultValue("// Generated by Interface Extractor on {Date} at {Time}\n// File: {FileName}")]
        public string FileHeaderTemplate
        {
            get => _options.FileHeaderTemplate;
            set => _options.FileHeaderTemplate = value;
        }

        [Category("Templates")]
        [DisplayName("Member Separator Lines")]
        [Description("Number of blank lines between interface members (0-3).")]
        [DefaultValue(1)]
        public int MemberSeparatorLines
        {
            get => _options.MemberSeparatorLines;
            set => _options.MemberSeparatorLines = value;
        }

        [Category("Templates")]
        [DisplayName("Sort Members")]
        [Description("Sort interface members alphabetically by type and name.")]
        [DefaultValue(false)]
        public bool SortMembers
        {
            get => _options.SortMembers;
            set => _options.SortMembers = value;
        }

        [Category("Templates")]
        [DisplayName("Group By Member Type")]
        [Description("Group interface members by type (Properties, Methods, Events, Indexers, Operators).")]
        [DefaultValue(false)]
        public bool GroupByMemberType
        {
            get => _options.GroupByMemberType;
            set => _options.GroupByMemberType = value;
        }

        /// <summary>
        /// Gets the underlying options data model
        /// </summary>
        public ExtractorOptions GetOptions()
        {
            return _options;
        }

        /// <summary>
        /// Validates settings when applied
        /// </summary>
        protected override void OnApply(PageApplyEventArgs e)
        {
            // Validate folder name
            if (string.IsNullOrWhiteSpace(InterfacesFolderName))
            {
                e.ApplyBehavior = ApplyKind.CancelNoNavigate;
                return;
            }

            // Validate prefix
            if (InterfacePrefix != null && InterfacePrefix.Length > 5)
            {
                e.ApplyBehavior = ApplyKind.CancelNoNavigate;
                return;
            }

            // Validate separator lines
            if (MemberSeparatorLines < 0 || MemberSeparatorLines > 3)
            {
                MemberSeparatorLines = 1;
            }

            // Validate implementation stub suffix
            if (string.IsNullOrWhiteSpace(ImplementationStubSuffix))
            {
                ImplementationStubSuffix = "Implementation";
            }

            base.OnApply(e);
        }
    }

    /// <summary>
    /// Options data model - can be used without Visual Studio dependencies
    /// </summary>
    public class ExtractorOptions
    {
        public string InterfacesFolderName { get; set; } = "Interfaces";
        public string InterfacePrefix { get; set; } = "I";
        public string InterfacesNamespaceSuffix { get; set; } = ".Interfaces";
        public bool AutoUpdateClass { get; set; } = true;
        public bool AddUsingDirective { get; set; } = true;
        public bool WarnIfNoIPrefix { get; set; } = true;
        public bool IncludeOperatorOverloads { get; set; } = false;
        public bool ShowPreviewBeforeSaving { get; set; } = true;
        public bool IncludeInternalMembers { get; set; } = false;
        public bool AnalyzePartialClasses { get; set; } = true;
        public bool GenerateImplementationStubs { get; set; } = false;
        public string ImplementationStubSuffix { get; set; } = "Implementation";
        public bool IncludeFileHeader { get; set; } = false;
        public string FileHeaderTemplate { get; set; } = "// Generated by Interface Extractor on {Date} at {Time}\n// File: {FileName}";
        public int MemberSeparatorLines { get; set; } = 1;
        public bool SortMembers { get; set; } = false;
        public bool GroupByMemberType { get; set; } = false;
    }

    /// <summary>
    /// Static accessor for options
    /// </summary>
    public static class OptionsProvider
    {
        private static GeneralOptionsPage _optionsPage;

        public static ExtractorOptions GetOptions(Package package)
        {
            if (_optionsPage == null && package != null)
            {
                _optionsPage = (GeneralOptionsPage)package.GetDialogPage(typeof(GeneralOptionsPage));
            }
            return _optionsPage?.GetOptions() ?? new ExtractorOptions();
        }

        public static void ClearCache()
        {
            _optionsPage = null;
        }
    }
}