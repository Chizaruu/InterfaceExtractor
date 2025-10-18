# Changelog

All notable changes to the Interface Extractor extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2025-10-18

### Added

- **Interface Preview Dialog**: New preview window showing the complete generated interface before saving to disk
  - View the full interface code with syntax highlighting
  - See the exact file path where the interface will be saved
  - Option to save or cancel the operation
  - Can be disabled in options (Tools → Options → Interface Extractor → Behavior → Show Preview Before Saving)

- **Multi-file Partial Class Support**: Automatically analyzes all partial class files across the project
  - Scans project directory for other partial class definitions
  - Combines members from all partial files into a single interface
  - Avoids duplicate member signatures
  - Controlled by option: "Analyze Partial Classes" (enabled by default)
  - Logs which partial files were discovered and analyzed

- **Record Type Support**: Extract interfaces from C# record types
  - Works with both `record` and `record class` declarations
  - Properly handles record properties and methods
  - Auto-detects record types and includes them in analysis
  - Updates records to implement interfaces (respects Auto Update Class option)
  - Converts `init` accessors to `get` in interfaces (since `init` is not valid in interface declarations)

- **Internal Member Support**: Optionally include internal members in interface extraction
  - New option: "Include Internal Members" (disabled by default)
  - Includes both explicitly internal and implicitly internal (no accessor) members
  - Works for methods, properties, events, and indexers
  - Useful for internal-facing interfaces

- **Implementation Stub Generation**: Automatically create skeleton implementation classes
  - New option: "Generate Implementation Stubs" (disabled by default)
  - Generates class with NotImplementedException for methods
  - Auto-implements properties with { get; set; }
  - Configurable suffix for stub class names (default: "Implementation")
  - Stubs placed in same Interfaces folder as the interface
  - Preserves XML documentation from interface

### Changed

- Enhanced logging output with version number and detailed operation tracking
- Improved type declaration handling to support both classes and records uniformly
- Service method `AnalyzeClassesAsync` now accepts optional `projectDirectory` parameter for partial class scanning
- Better accessibility detection with `IsAccessible()` helper method
- Command execution now passes project directory to service for enhanced analysis

### Fixed

- Better handling of implicit internal accessibility for type declarations
- Improved project item addition for generated stub files
- More robust error handling during partial file analysis

### Technical Details

- Added `PreviewDialog.xaml` and `PreviewDialog.xaml.cs` for interface preview UI
- Added `IsPartial` and `IsRecord` properties to `ExtractedClassInfo`
- New `GenerateImplementationStub` method in `InterfaceExtractorService`
- New `AnalyzePartialClassFiles` private method for multi-file scanning
- Enhanced `ExtractPublicMembers` to handle both classes and records via `TypeDeclarationSyntax`
- Options model extended with 5 new settings

## [1.1.0] - 2025-10-18

### Added

- Comprehensive options page accessible through Tools → Options → Interface Extractor → General
- Support for operator overloads (optional, disabled by default)
- Custom file header templates with `{FileName}`, `{Date}`, `{Time}` placeholders
- Member sorting options (sort alphabetically by type and name)
- Member grouping options (group by Properties, Methods, Events, etc.)
- Configurable member separator lines (0-3 blank lines between members)
- "Add Using Directive" option for interface namespace imports
- "Warn If No 'I' Prefix" option for interface naming validation
- Automatic class update option (add interface to class declaration)

### Changed

- Moved configuration from hardcoded constants to user-editable options
- Improved namespace handling to avoid self-referencing using statements
- Enhanced interface generation with optional sorting and grouping
- Better documentation preservation for all member types

### Technical Details

- Added `OptionsPage.cs` with `GeneralOptionsPage` and `ExtractorOptions` classes
- Added `OptionsProvider` static class for options access throughout the extension
- Enhanced `InterfaceExtractorService` constructor to accept options
- Modified `GenerateInterface` to use template options
- Added support for `OperatorDeclarationSyntax` and `ConversionOperatorDeclarationSyntax`

## [1.0.0] - 2025-10-18

### Added

- Initial release of Interface Extractor extension
- Right-click context menu integration in Solution Explorer for .cs files
- Interactive member selection dialog with checkboxes
- Automatic interface name suggestion with 'I' prefix
- Support for extracting:
  - Public methods (including generic methods with constraints)
  - Public properties (with correct { get; set; } detection)
  - Public events
  - Public indexers
- XML documentation comment preservation
- Generic method constraint handling
- Automatic interface file creation in "Interfaces" subfolder
- Automatic namespace suffix (.Interfaces)
- File overwrite protection with Yes/No/Yes to All/No to All options
- Optional automatic class update to implement generated interface
- Detailed logging to Visual Studio Output Window
- Batch processing support for multiple files

### Technical Details

- Built on Visual Studio SDK 17.14
- Uses Microsoft.CodeAnalysis.CSharp (Roslyn) for syntax analysis
- Targets .NET Framework 4.8
- WPF-based user interface dialogs
- VSIX package format for distribution

### Known Limitations

- Only processes public, non-static members
- Does not analyze nested classes
- Partial classes: only processes current file
- Operator overloads not included by default

---

## Version Number Scheme

Interface Extractor follows [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API changes
- **MINOR** version for new functionality in a backward compatible manner
- **PATCH** version for backward compatible bug fixes

Example: Version 1.2.0
- **1** = Major version (initial release)
- **2** = Minor version (new features: preview, partial classes, records, stubs, internal)
- **0** = Patch version (no patches yet for this minor version)