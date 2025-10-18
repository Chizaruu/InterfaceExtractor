using FluentAssertions;
using InterfaceExtractor.UI;
using Xunit;

namespace InterfaceExtractor.Tests.UI
{
    /// <summary>
    /// Tests for Preview Dialog
    /// </summary>
    public class PreviewDialogTests
    {
        [StaFact]
        public void Constructor_InitializesWithValues()
        {
            // Arrange
            var interfaceName = "ITestClass";
            var filePath = @"C:\Temp\Interfaces\ITestClass.cs";
            var interfaceCode = @"
namespace Test.Interfaces
{
    public interface ITestClass
    {
        string Name { get; set; }
    }
}";
            // Act
            var dialog = new PreviewDialog(interfaceName, filePath, interfaceCode);

            // Assert
            dialog.Should().NotBeNull();
            dialog.UserApproved.Should().BeFalse(); // Default value
        }

        [StaFact]
        public void Constructor_SetsUserApprovedToFalse()
        {
            // Arrange & Act
            var dialog = new PreviewDialog("ITest", "C:\\test.cs", "code");

            // Assert
            dialog.UserApproved.Should().BeFalse();
        }

        [StaTheory]
        [InlineData("ISimpleClass", @"C:\Projects\Test\Interfaces\ISimpleClass.cs")]
        [InlineData("IUserService", @"C:\Dev\MyApp\Interfaces\IUserService.cs")]
        [InlineData("IDataRepository", @"C:\Temp\IDataRepository.cs")]
        public void Constructor_AcceptsDifferentFileNames(string interfaceName, string filePath)
        {
            // Arrange & Act
            var dialog = new PreviewDialog(interfaceName, filePath, "test code");

            // Assert
            dialog.Should().NotBeNull();
        }

        [StaFact]
        public void Constructor_HandlesLongInterfaceCode()
        {
            // Arrange
            var longCode = string.Join("\n", System.Linq.Enumerable.Repeat("// Line of code", 1000));

            // Act
            var dialog = new PreviewDialog("ILargeInterface", "C:\\test.cs", longCode);

            // Assert
            dialog.Should().NotBeNull();
        }

        [StaFact]
        public void Constructor_HandlesEmptyInterfaceCode()
        {
            // Arrange & Act
            var dialog = new PreviewDialog("IEmpty", "C:\\empty.cs", string.Empty);

            // Assert
            dialog.Should().NotBeNull();
        }

        [StaFact]
        public void Constructor_HandlesSpecialCharactersInCode()
        {
            // Arrange
            var codeWithSpecialChars = @"
namespace Test
{
    public interface ITest
    {
        // Comment with special chars: <>&""'
        string Property { get; set; }
    }
}";
            // Act
            var dialog = new PreviewDialog("ITest", "C:\\test.cs", codeWithSpecialChars);

            // Assert
            dialog.Should().NotBeNull();
        }
    }
}