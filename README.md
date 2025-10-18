# Interface Extractor

A Visual Studio 2022 extension that extracts interfaces from C# classes with interactive member selection.

## Features

✨ **Right-click to extract** - Works directly from Solution Explorer context menu  
✨ **Interactive selection** - Choose which methods, properties, events, and indexers to include  
✨ **Smart defaults** - Auto-suggests interface names with "I" prefix  
✨ **Batch processing** - Handle multiple files at once  
✨ **XML documentation** - Preserves XML comments from original members  
✨ **Generic support** - Correctly handles generic methods with constraints  
✨ **Read-only properties** - Properly detects `{ get; }` vs `{ get; set; }`  
✨ **Overwrite protection** - Prompts before replacing existing files  
✨ **Detailed logging** - Progress tracking in Output Window

### Supported Members

- Public methods (including generic methods with constraints)
- Public properties (with correct accessor detection)
- Public events
- Public indexers

**Note:** Static members and private members are excluded by design.

## Requirements

- Visual Studio 2022 (Community, Professional, or Enterprise)
- .NET Framework 4.8

## Installation

### Option 1: Build from Source

1. Install the **Visual Studio extension development** workload via Visual Studio Installer
2. Clone or download this repository
3. Open `InterfaceExtractor.sln` in Visual Studio 2022
4. Build the solution (Ctrl+Shift+B)
5. Close all Visual Studio instances
6. Run the generated `.vsix` file from `bin/Debug/` or `bin/Release/`
7. Restart Visual Studio

### Option 2: From VSIX Package

1. Download the `.vsix` file
2. Close all Visual Studio instances
3. Double-click the `.vsix` file
4. Follow the installation wizard
5. Restart Visual Studio

## Usage

### Quick Start

1. Right-click any `.cs` file in Solution Explorer
2. Select **Extract Interface...**
3. Review/edit the interface name (defaults to `I{ClassName}`)
4. Select members to include
5. Click **OK**

The extension creates an `Interfaces` folder, generates the interface file, and adds it to your project.

### Member Selection Dialog

- ✏️ **Edit interface name** - Modify the suggested name
- ☑️ **Select/deselect members** - Pick which members to include
- 🔘 **Select All / Deselect All** - Quick selection buttons
- 👁️ **Preview signatures** - See exact interface signatures

### View Progress

1. Open Output Window (View → Output or Ctrl+Alt+O)
2. Select **Interface Extractor** from the dropdown
3. View detailed logs of the extraction process

## Examples

### Basic Class with Properties and Methods

**Input (BookingData.cs):**

```csharp
namespace MyProject.Data
{
    /// <summary>
    /// Manages booking operations
    /// </summary>
    public class BookingData
    {
        /// <summary>
        /// Gets a booking by ID
        /// </summary>
        public async Task<Booking> GetBookingAsync(Guid id) { }
        
        /// <summary>
        /// Gets or sets the booking name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets the booking date (read-only)
        /// </summary>
        public DateTime BookingDate { get; }
        
        private void InternalMethod() { } // Excluded (private)
    }
}
```

**Output (Interfaces/IBookingData.cs):**

```csharp
namespace MyProject.Data.Interfaces
{
    /// <summary>
    /// Manages booking operations
    /// </summary>
    public interface IBookingData
    {
        /// <summary>
        /// Gets a booking by ID
        /// </summary>
        Task<Booking> GetBookingAsync(Guid id);

        /// <summary>
        /// Gets or sets the booking name
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets the booking date (read-only)
        /// </summary>
        DateTime BookingDate { get; }
    }
}
```

### Generic Methods with Constraints

**Input:**

```csharp
public class Repository
{
    public T GetById<T>(int id) where T : class, IEntity, new() { }
    
    public List<T> GetAll<T>() where T : IEntity { }
}
```

**Output:**

```csharp
public interface IRepository
{
    T GetById<T>(int id)
        where T : class, IEntity, new();

    List<T> GetAll<T>()
        where T : IEntity;
}
```

### Events and Indexers

**Input:**

```csharp
public class DataCollection
{
    public event EventHandler<DataChangedEventArgs> DataChanged;
    
    public string this[int index]
    {
        get { return items[index]; }
        set { items[index] = value; }
    }
    
    public int Count { get; }
}
```

**Output:**

```csharp
public interface IDataCollection
{
    event EventHandler<DataChangedEventArgs> DataChanged;

    string this[int index] { get; set; }

    int Count { get; }
}
```

## Configuration

Default settings (modifiable in `Constants.cs`):

| Setting | Default Value | Description |
|---------|---------------|-------------|
| Interface Folder | `Interfaces` | Where interface files are created |
| Interface Prefix | `I` | Suggested prefix for interface names |
| Namespace Suffix | `.Interfaces` | Added to original namespace |

## Troubleshooting

### Extension doesn't appear in context menu

- Verify you're right-clicking `.cs` files
- Check Extensions → Manage Extensions to confirm it's installed and enabled
- Restart Visual Studio

### Build errors

- Ensure Visual Studio SDK is installed
- Restore NuGet packages (right-click solution → Restore NuGet Packages)
- Verify target framework is .NET Framework 4.8

### Interface not generated correctly

- Check Output Window (View → Output) and select "Interface Extractor"
- Ensure the class is `public`
- Verify the file contains valid C# syntax
- Check that at least one public member exists

### Validation errors in dialog

- Interface names must be valid C# identifiers
- Cannot use reserved keywords (`class`, `interface`, `void`, etc.)
- At least one member must be selected
- Names should start with a letter or underscore

## Project Structure

```txt
InterfaceExtractor/
├── Commands/
│   └── ExtractInterfaceCommand.cs      # Command handler and orchestration
├── Services/
│   └── InterfaceExtractorService.cs    # Roslyn-based extraction logic
├── UI/
│   ├── ExtractInterfaceDialog.xaml     # Member selection dialog
│   ├── ExtractInterfaceDialog.xaml.cs
│   ├── OverwriteDialog.xaml            # File overwrite confirmation
│   ├── OverwriteDialog.xaml.cs
│   └── MemberSelectionItem.cs          # View model for members
├── Constants.cs                         # Configuration constants
├── InterfaceExtractorPackage.cs        # VS Package entry point
└── InterfaceExtractorPackage.vsct      # Command definitions
```

## Development

### Debugging

1. Open `InterfaceExtractor.sln` in Visual Studio 2022
2. Press **F5** to launch experimental instance
3. Open a test project in the experimental instance
4. Test the extension
5. Check Output Window → "Interface Extractor" for logs

### Key Technologies

- **Roslyn (Microsoft.CodeAnalysis)** - C# syntax parsing and analysis
- **Visual Studio SDK** - IDE integration
- **WPF** - User interface dialogs
- **VSIX** - Extension packaging format

## Known Limitations

- Only processes public, non-static members
- Does not support operator overloads
- Partial classes: only analyzes the current file
- Nested classes: only processes top-level classes
- No configuration UI (requires source modification)

## Roadmap

### Planned for v1.1

- [ ] Options/settings page
- [ ] Support for operator overloads
- [ ] Custom interface templates
- [ ] Automatic class implementation updates

### Planned for v2.0

- [ ] Multi-file partial class support
- [ ] Interface preview before saving
- [ ] Integration with VS refactoring tools
- [ ] Support for internal members (optional)

## Version History

See [CHANGELOG.md](CHANGELOG.md) for detailed version history.

**v1.0.0** (2025-10-18) - Initial release with full feature set

## License

MIT License

## Contributing

Contributions welcome! Areas for improvement:

- Configuration UI
- Additional member type support (operators)
- Advanced namespace handling
- Template customization system
- Better integration with VS refactoring

## Support

For issues or questions:

1. Check the **Output Window** (View → Output → "Interface Extractor") for detailed error messages
2. Review the **Troubleshooting** section above
3. Verify Visual Studio 2022 and .NET Framework 4.8 compatibility
4. Create an issue with:
   - Visual Studio version
   - Steps to reproduce
   - Output Window logs
   - Sample code (if applicable)

---

**Made with ❤️ for Visual Studio developers**
