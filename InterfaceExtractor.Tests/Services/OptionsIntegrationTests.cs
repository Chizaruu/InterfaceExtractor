using FluentAssertions;
using InterfaceExtractor.Options;
using InterfaceExtractor.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace InterfaceExtractor.Tests.Services
{
    public class OptionsIntegrationTests : IDisposable
    {
        private readonly string _tempDirectory;
        private bool _disposed;

        public OptionsIntegrationTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"OptionsTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && Directory.Exists(_tempDirectory))
                {
                    try { Directory.Delete(_tempDirectory, true); }
                    catch { /* Ignore cleanup errors */ }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region Options Tests

        [Fact]
        public void ExtractorOptions_HasCorrectDefaults()
        {
            // Arrange & Act
            var options = new ExtractorOptions();

            // Assert
            options.InterfacesFolderName.Should().Be("Interfaces");
            options.InterfacePrefix.Should().Be("I");
            options.InterfacesNamespaceSuffix.Should().Be(".Interfaces");
            options.AutoUpdateClass.Should().BeTrue();
            options.AddUsingDirective.Should().BeTrue();
            options.WarnIfNoIPrefix.Should().BeTrue();
            options.IncludeOperatorOverloads.Should().BeFalse();
            options.IncludeFileHeader.Should().BeFalse();
            options.MemberSeparatorLines.Should().Be(1);
            options.SortMembers.Should().BeFalse();
            options.GroupByMemberType.Should().BeFalse();
        }

        [Fact]
        public void ExtractorOptions_CanModifySettings()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                // Act
                InterfacesFolderName = "Contracts",
                InterfacePrefix = "X",
                InterfacesNamespaceSuffix = ".Contracts",
                AutoUpdateClass = false,
                IncludeOperatorOverloads = true
            };

            // Assert
            options.InterfacesFolderName.Should().Be("Contracts");
            options.InterfacePrefix.Should().Be("X");
            options.InterfacesNamespaceSuffix.Should().Be(".Contracts");
            options.AutoUpdateClass.Should().BeFalse();
            options.IncludeOperatorOverloads.Should().BeTrue();
        }

        [Fact]
        public async Task Service_RespectsCustomFolderName()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                InterfacesFolderName = "CustomFolder"
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);

            // Assert - Folder name doesn't affect namespace, only the namespace suffix does
            interfaceCode.Should().Contain("namespace Test.Interfaces"); // Still uses .Interfaces suffix
        }

        [Fact]
        public async Task Service_RespectsCustomNamespaceSuffix()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                InterfacesNamespaceSuffix = ".Contracts"
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace MyApp.Services
{
    public class UserService
    {
        public string GetUser() { return null; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface("IUserService", classInfos[0], classInfos[0].Members);

            // Assert
            interfaceCode.Should().Contain("namespace MyApp.Services.Contracts");
        }

        [Fact]
        public void Service_WithAutoUpdateDisabled_DoesNotUpdateClass()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                AutoUpdateClass = false
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = service.AppendInterfaceToClass(sourceCode, "TestClass", "ITestClass", "Test.Interfaces");

            // Assert
            result.Should().Be(sourceCode); // Unchanged
        }

        [Fact]
        public void Service_WithAddUsingDirectiveDisabled_UsesFullyQualifiedName()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                AutoUpdateClass = true,
                AddUsingDirective = false
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = service.AppendInterfaceToClass(sourceCode, "TestClass", "ITestClass", "Test.Interfaces");

            // Assert - Should use fully qualified name since no using directive
            result.Should().Contain("public class TestClass : Test.Interfaces.ITestClass");
            result.Should().NotContain("using Test.Interfaces;");
        }

        [Fact]
        public void Service_WithAddUsingDirectiveEnabled_UsesSimpleNameAndAddsUsing()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                AutoUpdateClass = true,
                AddUsingDirective = true
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = service.AppendInterfaceToClass(sourceCode, "TestClass", "ITestClass", "Test.Interfaces");

            // Assert - Should use simple name and add using directive
            result.Should().Contain("public class TestClass : ITestClass");
            result.Should().NotContain("Test.Interfaces.ITestClass");
            result.Should().Contain("using Test.Interfaces;");
        }

        #endregion Options Tests

        #region Operator Overload Tests

        [Fact]
        public async Task Service_WithOperatorsDisabled_ExcludesOperators()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeOperatorOverloads = false // Default
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class Vector
    {
        public double X { get; set; }

        public static Vector operator +(Vector a, Vector b)
        {
            return null;
        }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Assert
            classInfos[0].Members.Should().HaveCount(1); // Only property, no operator
            classInfos[0].Members[0].Type.Should().Be(MemberType.Property);
        }

        [Fact]
        public async Task Service_WithOperatorsEnabled_IncludesOperators()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeOperatorOverloads = true
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class Vector
    {
        public double X { get; set; }

        public static Vector operator +(Vector a, Vector b)
        {
            return null;
        }

        public static Vector operator -(Vector a, Vector b)
        {
            return null;
        }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Assert
            classInfos[0].Members.Should().HaveCount(3); // Property + 2 operators
            classInfos[0].Members.Should().Contain(m => m.Type == MemberType.Operator && m.Name.Contains("+"));
            classInfos[0].Members.Should().Contain(m => m.Type == MemberType.Operator && m.Name.Contains("-"));
        }

        [Fact]
        public async Task Service_ExtractsAllOperatorTypes()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeOperatorOverloads = true
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class ComplexNumber
    {
        public double Real { get; set; }
        public double Imaginary { get; set; }

        // Binary operators
        public static ComplexNumber operator +(ComplexNumber a, ComplexNumber b) { return null; }
        public static ComplexNumber operator -(ComplexNumber a, ComplexNumber b) { return null; }
        public static ComplexNumber operator *(ComplexNumber a, ComplexNumber b) { return null; }
        public static ComplexNumber operator /(ComplexNumber a, ComplexNumber b) { return null; }

        // Comparison operators
        public static bool operator ==(ComplexNumber a, ComplexNumber b) { return true; }
        public static bool operator !=(ComplexNumber a, ComplexNumber b) { return false; }

        // Unary operators
        public static ComplexNumber operator -(ComplexNumber a) { return null; }
        public static ComplexNumber operator !(ComplexNumber a) { return null; }

        // Conversion operators
        public static implicit operator string(ComplexNumber c) { return null; }
        public static explicit operator double(ComplexNumber c) { return 0; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Assert
            var operators = classInfos[0].Members.Where(m => m.Type == MemberType.Operator).ToList();
            operators.Should().HaveCount(10);
            operators.Should().Contain(o => o.Signature.Contains("operator +"));
            operators.Should().Contain(o => o.Signature.Contains("operator =="));
            operators.Should().Contain(o => o.Signature.Contains("implicit operator"));
            operators.Should().Contain(o => o.Signature.Contains("explicit operator"));
        }

        #endregion Operator Overload Tests

        #region Template Tests

        [Fact]
        public async Task Service_WithFileHeader_IncludesHeader()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeFileHeader = true,
                FileHeaderTemplate = "// Custom Header\n// File: {FileName}"
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);

            // Assert
            interfaceCode.Should().StartWith("// Custom Header");
            interfaceCode.Should().Contain("// File: ITestClass.cs");
        }

        [Fact]
        public async Task Service_WithGrouping_GroupsMembersByType()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                GroupByMemberType = true
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
using System;

namespace Test
{
    public class TestClass
    {
        public void Method1() { }
        public string Property1 { get; set; }
        public void Method2() { }
        public int Property2 { get; set; }
        public event EventHandler Event1;
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);

            // Assert
            interfaceCode.Should().Contain("// Properties");
            interfaceCode.Should().Contain("// Methods");
            interfaceCode.Should().Contain("// Events");

            // Properties should come before methods (based on GetMemberTypeOrder)
            var propertiesIndex = interfaceCode.IndexOf("// Properties");
            var methodsIndex = interfaceCode.IndexOf("// Methods");
            propertiesIndex.Should().BeLessThan(methodsIndex);
        }

        [Fact]
        public async Task Service_WithSorting_SortsMembersAlphabetically()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                SortMembers = true
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class TestClass
    {
        public string Zebra { get; set; }
        public string Alpha { get; set; }
        public string Beta { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);

            // Assert
            var alphaIndex = interfaceCode.IndexOf("string Alpha");
            var betaIndex = interfaceCode.IndexOf("string Beta");
            var zebraIndex = interfaceCode.IndexOf("string Zebra");

            alphaIndex.Should().BeLessThan(betaIndex);
            betaIndex.Should().BeLessThan(zebraIndex);
        }

        [Fact]
        public async Task Service_WithZeroSeparatorLines_HasNoBlankLinesBetweenMembers()
        {
            // Arrange
            var options = new ExtractorOptions { MemberSeparatorLines = 0 };
            var service = new InterfaceExtractorService(options);

            var sourceCode = "namespace Test { public class TestClass { public string A { get; set; } public string B { get; set; } } }";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);

            // Assert - with 0 separator lines, properties should be consecutive (no blank line between)
            var lines = interfaceCode.Split(["\r\n", "\n"], StringSplitOptions.None);
            var aIndex = Array.FindIndex(lines, l => l.Contains("string A"));
            var bIndex = Array.FindIndex(lines, l => l.Contains("string B"));

            // Count blank lines between them
            var blankLines = 0;
            for (int i = aIndex + 1; i < bIndex; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    blankLines++;
                }
            }

            blankLines.Should().Be(0, "with 0 separator lines, there should be no blank lines between members");
        }

        [Fact]
        public async Task Service_WithMultipleSeparatorLines_AddsBlankLinesBetweenMembers()
        {
            // Arrange
            var optionsWithOne = new ExtractorOptions { MemberSeparatorLines = 1 };
            var optionsWithThree = new ExtractorOptions { MemberSeparatorLines = 3 };

            var serviceOne = new InterfaceExtractorService(optionsWithOne);
            var serviceThree = new InterfaceExtractorService(optionsWithThree);

            var sourceCode = "namespace Test { public class TestClass { public string A { get; set; } public string B { get; set; } } }";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await serviceOne.AnalyzeClassesAsync(filePath);

            // Act
            var codeWithOne = serviceOne.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);
            var codeWithThree = serviceThree.GenerateInterface("ITestClass", classInfos[0], classInfos[0].Members);

            // Assert - Count blank lines between the properties
            var linesOne = codeWithOne.Split(["\r\n", "\n"], StringSplitOptions.None);
            var linesThree = codeWithThree.Split(["\r\n", "\n"], StringSplitOptions.None);

            var aIndexOne = Array.FindIndex(linesOne, l => l.Contains("string A"));
            var bIndexOne = Array.FindIndex(linesOne, l => l.Contains("string B"));

            var aIndexThree = Array.FindIndex(linesThree, l => l.Contains("string A"));
            var bIndexThree = Array.FindIndex(linesThree, l => l.Contains("string B"));

            // Count blank lines between them
            var blankLinesOne = 0;
            for (int i = aIndexOne + 1; i < bIndexOne; i++)
            {
                if (string.IsNullOrWhiteSpace(linesOne[i]))
                {
                    blankLinesOne++;
                }
            }

            var blankLinesThree = 0;
            for (int i = aIndexThree + 1; i < bIndexThree; i++)
            {
                if (string.IsNullOrWhiteSpace(linesThree[i]))
                {
                    blankLinesThree++;
                }
            }

            blankLinesOne.Should().Be(1, "with MemberSeparatorLines=1, there should be 1 blank line");
            blankLinesThree.Should().Be(3, "with MemberSeparatorLines=3, there should be 3 blank lines");
        }

        #endregion Template Tests

        #region Helper Methods

        private string CreateTempFile(string content)
        {
            var fileName = $"Test_{Guid.NewGuid()}.cs";
            var filePath = Path.Combine(_tempDirectory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        #endregion Helper Methods
    }
}