# Changelog

All notable changes to the Interface Extractor extension will be documented in this file.

## [1.0.0] - 2025-10-18

### Added
- ✨ **Interactive member selection dialog** - Choose exactly which members to include
- ✨ **Multiple class support** - Handles files with multiple public classes
- ✨ **Event support** - Extract public events into interfaces
- ✨ **Indexer support** - Extract public indexers into interfaces
- ✨ **XML documentation preservation** - Copies XML comments from original members to interface
- ✨ **Output Window logging** - Detailed progress and error messages
- ✨ **Input validation** - Validates interface names as valid C# identifiers
- ✨ **Reserved keyword protection** - Prevents using C# keywords as interface names
- ✨ **File overwrite protection** - Prompts before overwriting existing interfaces
- ✨ **Constants file** - Centralized configuration constants
- ✨ **Select All/Deselect All** - Quick member selection in dialog
- ✨ **Indeterminate checkbox state** - Shows when some (but not all) members are selected

### Fixed
- 🐛 **Property accessor generation** - Now correctly handles read-only properties
  - Previously: `string Name { get; }` became `string Name { get; set; }`
  - Now: Correctly generates `string Name { get; }`
- 🐛 **Private accessor handling** - Excludes private setters and getters from interfaces
- 🐛 **Expression-bodied properties** - Correctly identifies as read-only
- 🐛 **Exception handling** - Comprehensive error handling and logging
  - No more silent failures
  - All errors logged to Output Window
  - User-friendly error messages
- 🐛 **File already in project** - Handles cases where interface file already exists in project

### Improved
- 🚀 **Better error messages** - More descriptive and actionable error messages
- 🚀 **Comprehensive logging** - Every operation logged with timestamps
- 🚀 **Multiple class handling** - Properly processes files with multiple public classes
- 🚀 **Batch processing feedback** - Clear success/failure/skip counts
- 🚀 **Dialog UX** - Better validation and user feedback in selection dialog
- 🚀 **Code organization** - Added Constants.cs for maintainability

### Technical Improvements
- Refactored `AnalyzeClassAsync` to `AnalyzeClassesAsync` (returns list)
- Added `IsValidInterfaceName` method with proper C# identifier validation
- Added Output Window pane initialization
- Added `ShowConfirmation` method for user prompts
- Added `LogMessage` method for structured logging
- Better null checking and exception handling throughout
- Added XML documentation to generated interfaces

### Developer Experience
- 📝 **Updated README** - Comprehensive documentation with examples
- 📝 **Created CHANGELOG** - Track all changes
- 📝 **Added AssemblyInfo.cs** - Proper assembly metadata
- 🧪 **Testing checklist** - Documented scenarios to test before release

### Breaking Changes
None - this is the initial production-ready release.

---

## Future Roadmap

### Planned for 1.1.0
- [ ] Options page for configuration
- [ ] Support for operator overloads
- [ ] Custom template system
- [ ] Interface implementation insertion
- [ ] Support for internal members (optional)

### Planned for 1.2.0
- [ ] Multi-file partial class support
- [ ] Refactoring integration
- [ ] Interface preview before saving
- [ ] Batch rename interfaces
- [ ] Smart using directive filtering

### Planned for 2.0.0
- [ ] Support for Visual Studio 2024
- [ ] .NET 8 target framework
- [ ] Async dialog support
- [ ] Integration with VS refactoring tools
- [ ] Extension API for customization

---

## Version Number Scheme

This project follows [Semantic Versioning](https://semver.org/):
- **MAJOR** version for incompatible API changes
- **MINOR** version for added functionality (backwards compatible)
- **PATCH** version for backwards compatible bug fixes

Given a version number MAJOR.MINOR.PATCH:
- 1.0.0 = Initial stable release
- 1.1.0 = New features, backwards compatible
- 1.0.1 = Bug fixes only
- 2.0.0 = Breaking changes or major overhaul