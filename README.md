# Interface Extractor - Visual Studio Extension

A Visual Studio extension that allows you to extract interfaces from C# classes directly from Solution Explorer with interactive member selection.

## Features

- **Right-click Context Menu** - Extract interfaces from any C# file in Solution Explorer
- **Interactive Member Selection** - Choose which methods, properties, events, and indexers to include
- **Smart Defaults** - Automatically suggests interface name with "I" prefix
- **Multiple Class Support** - Handles files with multiple public classes
- **Comprehensive Member Support**:
  - Public methods (including generic methods with constraints)
  - Public properties (with correct accessor detection)
  - Public events
  - Public indexers
- **Batch Processing** - Process multiple files at once
- **Output Logging** - Detailed progress and error messages in Output Window
- **File Overwrite Protection** - Prompts before overwriting existing interfaces
- **Input Validation** - Ensures valid C# identifiers and prevents reserved keywords
- **XML Documentation Preservation** - Copies XML comments from original members

## Requirements

- Visual Studio 2022 (Community, Professional, or Enterprise)
- .NET Framework 4.8
- Visual Studio SDK

## Installation

### Build from Source

1. **Install Visual Studio Extension Development workload**
   - Open Visual Studio Installer
   - Modify your VS 2022 installation
   - Select "Visual Studio extension development" workload
   - Install

2. **Clone or download the project files**

3. **Build the Extension**
   - Open `InterfaceExtractor.sln` in Visual Studio 2022
   - Build the solution (F6 or Ctrl+Shift+B)
   - The VSIX file will be created in `bin/Debug/` or `bin/Release/`

4. **Install the Extension**
   - Close all Visual Studio instances
   - Double-click the `.vsix` file
   - Follow the installation wizard
   - Restart Visual Studio

## Usage

### Basic Usage

1. Open any C# solution in Visual Studio
2. In Solution Explorer, right-click on one or more `.cs` files
3. Select **"Extract Interface..."** from the context menu
4. In the dialog:
   - Review the suggested interface name (defaults to `I{ClassName}`)
   - Select which members to include (all selected by default)
   - Click **OK**
5. The extension will:
   - Create an `Interfaces` folder (if it doesn't exist)
   - Generate the interface file (e.g., `IBookingData.cs`)
   - Add the file to your project
   - Show results in the Output Window

### Member Selection Dialog

The dialog allows you to:
- **Edit interface name** - Change the default `I{ClassName}` name
- **Select/deselect members** - Choose exactly which members to include
- **Select All / Deselect All** - Quick selection buttons
- **View member signatures** - See the exact interface signature for each member

### Viewing Progress

- Open the Output Window (View > Output or Ctrl+Alt+O)
- Select "Interface Extractor" from the dropdown
- View detailed logging of the extraction process

## Examples

### Example 1: Basic Class

**Before (BookingData.cs):**
```csharp
namespace MyProject.Data
{
    /// <summary>
    /// Manages booking data operations
    /// </summary>
    public class BookingData
    {
        /// <summary>
        /// Gets a booking by ID
        /// </summary>
        public async Task<Booking> GetBookingAsync(Guid id)
        {
            // implementation
        }
        
        /// <summary>
        /// Gets or sets the booking name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets the booking date (read-only)
        /// </summary>
        public DateTime BookingDate { get; }
        
        private void InternalMethod() { } // Not included (private)
    }
}
```

**After (Interfaces/IBookingData.cs):**
```csharp
namespace MyProject.Data.Interfaces
{
    /// <summary>
    /// Manages booking data operations
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

### Example 2: Generic Methods with Constraints

**Before:**
```csharp
public class Repository
{
    public T GetById<T>(int id) where T : class, IEntity, new()
    {
        // implementation
    }
    
    public List<T> GetAll<T>() where T : IEntity
    {
        // implementation
    }
}
```

**After:**
```csharp
public interface IRepository
{
    T GetById<T>(int id)
        where T : class, IEntity, new();

    List<T> GetAll<T>()
        where T : IEntity;
}
```

### Example 3: Events and Indexers

**Before:**
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

**After:**
```csharp
public interface IDataCollection
{
    event EventHandler<DataChangedEventArgs> DataChanged;

    string this[int index] { get; set; }

    int Count { get; }
}
```

## Configuration

The extension uses these defaults (can be modified in the source):
- **Interface Folder**: `Interfaces`
- **Interface Prefix**: `I`
- **Namespace Suffix**: `.Interfaces`

To change these defaults, modify the values in `Constants.cs` and rebuild.

## Troubleshooting

### Extension doesn't appear in context menu
- Ensure you're right-clicking on `.cs` files
- Restart Visual Studio
- Check Extensions > Manage Extensions to verify it's installed and enabled

### Build errors
- Ensure Visual Studio SDK is installed
- Check that all NuGet packages are restored (right-click solution > Restore NuGet Packages)
- Target Framework should be .NET Framework 4.8
- Visual Studio version should be 2022

### Interface not generated correctly
- Check Output Window (View > Output) and select "Interface Extractor" for detailed logs
- Ensure the class is public
- Verify the file contains valid C# code
- Check that at least one public member exists

### Dialog shows validation errors
- Interface names must be valid C# identifiers
- Cannot use C# reserved keywords (class, interface, etc.)
- At least one member must be selected

### File overwrite prompt
- The extension will prompt before overwriting existing interface files
- Choose "Yes" to overwrite or "No" to skip

## Development

### Project Structure

```
InterfaceExtractor/
├── Commands/
│   └── ExtractInterfaceCommand.cs    # Command handler and orchestration
├── Services/
│   └── InterfaceExtractorService.cs  # Core extraction logic using Roslyn
├── UI/
│   ├── ExtractInterfaceDialog.xaml   # Member selection dialog
│   ├── ExtractInterfaceDialog.xaml.cs
│   └── MemberSelectionItem.cs        # View model for members
├── Properties/
│   └── AssemblyInfo.cs               # Assembly metadata
├── Constants.cs                       # Shared constants
├── InterfaceExtractorPackage.cs      # VS Package entry point
└── InterfaceExtractorPackage.vsct    # Command definitions
```

### Debugging the Extension

1. Open the project in Visual Studio 2022
2. Press F5 to start debugging
3. A new "Experimental Instance" of Visual Studio will launch
4. Open your test project in the experimental instance
5. Test the extension
6. Check the Output Window for logging (select "Interface Extractor")

### Key Technologies

- **Roslyn (Microsoft.CodeAnalysis)** - C# syntax analysis
- **Visual Studio SDK** - VS integration and extensibility
- **WPF** - User interface (dialog)
- **VSIX** - Extension packaging

## Known Limitations

- Only processes public members
- Does not support operator overloads
- Static members are excluded
- Partial classes: only analyzes the current file
- Nested classes: only top-level classes are processed

## Future Enhancements

Potential improvements for future versions:
- Configuration options page
- Support for operator overloads
- Multi-file partial class support
- Template customization
- Interface implementation insertion into class
- Refactoring to use existing interfaces
- Support for internal members (with option)

## Version History

### 1.0.0 (2025-01-18)
- Initial release
- Basic interface extraction
- Interactive member selection
- Support for methods, properties, events, and indexers
- Batch processing support
- Output window logging
- Input validation
- File overwrite protection
- XML documentation preservation
- Multiple class support

## License

MIT License - Feel free to modify and use as needed.

## Support

For issues, questions, or suggestions:
1. Check the Output Window for detailed error messages
2. Review the troubleshooting section above
3. Check that your Visual Studio and .NET Framework versions are compatible

## Contributing

Contributions are welcome! Areas for improvement:
- Additional member type support
- Configuration UI
- More sophisticated namespace handling
- Template system for interface generation
- Integration with VS refactoring tools

---

**Happy coding! 🚀**