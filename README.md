# Interface Extractor - Visual Studio Extension

A Visual Studio extension that allows you to extract interfaces from C# classes directly from Solution Explorer.

## Features

- Right-click any C# file in Solution Explorer
- Select "Extract Interface..." from the context menu
- Automatically generates interface file in an "Interfaces" subfolder
- Extracts all public methods and properties
- Preserves using statements from the original file
- Handles generic methods with constraints
- Supports batch processing of multiple files
- Shows progress in Output Window

## Requirements

- Visual Studio 2022 (Community, Professional, or Enterprise)
- .NET Framework 4.8
- Visual Studio SDK

## Installation

### Option 1: Build and Install from Source

1. **Install Visual Studio Extension Development workload**
   - Open Visual Studio Installer
   - Modify your VS 2022 installation
   - Select "Visual Studio extension development" workload
   - Install

2. **Create the Extension Project**
   ```bash
   # Create new folder for the extension
   mkdir InterfaceExtractor
   cd InterfaceExtractor
   ```

3. **Add all the provided files to the project:**
   - `InterfaceExtractor.csproj`
   - `source.extension.vsixmanifest`
   - `InterfaceExtractorPackage.vsct`
   - `InterfaceExtractorPackage.cs`
   - `Commands/ExtractInterfaceCommand.cs`
   - `Services/InterfaceExtractorService.cs`
   - `Properties/AssemblyInfo.cs`

4. **Create Resources folder and add a placeholder icon:**
   - Create `Resources/ExtractInterface.png` (16x16 PNG)
   - Create `Resources/Icon.png` (32x32 PNG for extension icon)
   - Create `Resources/Preview.png` (200x200 PNG for marketplace)
   - Or use any placeholder images for now

5. **Create LICENSE.txt file**
   ```
   MIT License
   
   Copyright (c) 2025
   
   Permission is hereby granted, free of charge, to any person obtaining a copy
   of this software...
   ```

6. **Build the Extension**
   - Open `InterfaceExtractor.csproj` in Visual Studio 2022
   - Build the solution (F6)
   - The VSIX file will be created in `bin/Debug/` or `bin/Release/`

7. **Install the Extension**
   - Close all Visual Studio instances
   - Double-click the `.vsix` file
   - Follow the installation wizard
   - Restart Visual Studio

### Option 2: Quick Install (After Building)

Simply double-click the generated `.vsix` file in the `bin/Debug` or `bin/Release` folder.

## Usage

1. Open any C# solution in Visual Studio
2. In Solution Explorer, right-click on one or more `.cs` files
3. Select **"Extract Interface..."** from the context menu
4. The extension will:
   - Create an `Interfaces` folder (if it doesn't exist)
   - Generate interface files with `I` prefix (e.g., `IBookingData.cs`)
   - Add the files to your project
   - Show progress in the Output Window

## Example

**Before (BookingData.cs):**
```csharp
namespace MyProject.Data
{
    public class BookingData
    {
        public async Task<Booking> GetBookingAsync(Guid id)
        {
            // implementation
        }
        
        public string Name { get; set; }
    }
}
```

**After (Interfaces/IBookingData.cs):**
```csharp
namespace MyProject.Data.Interfaces
{
    public interface IBookingData
    {
        Task<Booking> GetBookingAsync(Guid id);
        string Name { get; set; }
    }
}
```

## Development

### Debugging the Extension

1. Open the project in Visual Studio 2022
2. Press F5 to start debugging
3. A new "Experimental Instance" of Visual Studio will launch
4. Open your test project in the experimental instance
5. Test the extension
6. Check the Output Window for any errors

### Modifying the Extension

- **Change menu text**: Edit `InterfaceExtractorPackage.vsct`
- **Modify extraction logic**: Edit `Services/InterfaceExtractorService.cs`
- **Change command behavior**: Edit `Commands/ExtractInterfaceCommand.cs`

## Troubleshooting

### Extension doesn't appear in context menu
- Ensure you're right-clicking on `.cs` files
- Restart Visual Studio
- Check Extensions > Manage Extensions to verify it's installed

### Build errors
- Ensure Visual Studio SDK is installed
- Check that all NuGet packages are restored
- Target Framework should be .NET Framework 4.8

### Interface not generated correctly
- Check Output Window (View > Output) for error messages
- Ensure the class is public
- Verify the file contains valid C# code

## Uninstalling

1. Go to Extensions > Manage Extensions
2. Find "Interface Extractor"
3. Click Uninstall
4. Restart Visual Studio

## License

MIT License - Feel free to modify and use as needed.

## Contributing

This is a basic implementation. Potential improvements:
- Add dialog to select which members to include
- Support for events and indexers
- Better handling of async methods
- Configuration options
- Support for multiple classes in one file

## Version History

### 1.0.0 (2025-01-17)
- Initial release
- Basic interface extraction
- Solution Explorer integration
- Batch processing support