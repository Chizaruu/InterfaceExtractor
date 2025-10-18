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
    /// <summary>
    /// Tests for record type support (v1.2.0)
    /// </summary>
    public class RecordTypeTests : IDisposable
    {
        private readonly InterfaceExtractorService _service;
        private readonly string _tempDirectory;
        private bool _disposed;

        public RecordTypeTests()
        {
            _service = new InterfaceExtractorService();
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"RecordTests_{Guid.NewGuid()}");
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

        [Fact]
        public async Task AnalyzeClassesAsync_WithSimpleRecord_ExtractsCorrectly()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public record Person(string FirstName, string LastName);
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(1);
            result[0].ClassName.Should().Be("Person");
            result[0].IsRecord.Should().BeTrue();
            result[0].Members.Should().HaveCount(2); // FirstName and LastName from primary constructor
            result[0].Members.Should().Contain(m => m.Name == "FirstName");
            result[0].Members.Should().Contain(m => m.Name == "LastName");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithRecordWithMethods_ExtractsAllMembers()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public record Person
    {
        public string FirstName { get; init; }
        public string LastName { get; init; }
        public DateTime BirthDate { get; init; }

        public int Age => (DateTime.Now - BirthDate).Days / 365;

        public string GetFullName() => $""{FirstName} {LastName}"";
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsRecord.Should().BeTrue();
            result[0].Members.Should().HaveCount(5); // 4 properties + 1 method
            result[0].Members.Should().Contain(m => m.Name == "FirstName");
            result[0].Members.Should().Contain(m => m.Name == "Age");
            result[0].Members.Should().Contain(m => m.Name == "GetFullName");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithRecordStruct_ExtractsCorrectly()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public record struct Point(int X, int Y);
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(1);
            result[0].ClassName.Should().Be("Point");
            result[0].IsRecord.Should().BeTrue();
            result[0].Members.Should().HaveCount(2); // X and Y from primary constructor
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithReadOnlyRecordProperties_DetectsCorrectly()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public record Product
    {
        public string Name { get; init; }
        public decimal Price { get; set; }
        public string Category { get; }

        public Product()
        {
            Category = ""Default"";
        }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var members = result[0].Members;

            // init accessor is converted to get for interfaces
            members.First(m => m.Name == "Name").Signature.Should().Contain("{ get; }");
            members.First(m => m.Name == "Price").Signature.Should().Contain("{ get; set; }");
            members.First(m => m.Name == "Category").Signature.Should().Contain("{ get; }");
        }

        [Fact]
        public async Task GenerateInterface_FromRecord_CreatesValidInterface()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public record Person(string FirstName, string LastName)
    {
        public int Age { get; set; }
        public string GetFullName() => $""{FirstName} {LastName}"";
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = _service.GenerateInterface(
                "IPerson",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            interfaceCode.Should().Contain("public interface IPerson");
            // Primary constructor parameters
            interfaceCode.Should().Contain("string FirstName { get; }");
            interfaceCode.Should().Contain("string LastName { get; }");
            // Explicit properties and methods
            interfaceCode.Should().Contain("int Age { get; set; }");
            interfaceCode.Should().Contain("string GetFullName();");
        }

        [Fact]
        public void AppendInterfaceToRecord_UpdatesRecordDeclaration()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public record Person(string FirstName, string LastName);
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "Person",
                "IPerson",
                "Test.Interfaces");

            // Assert
            result.Should().Contain("public record Person");
            result.Should().Contain(": IPerson");
            result.Should().Contain("using Test.Interfaces;");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_MixedClassesAndRecords_ExtractsBoth()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public class UserService
    {
        public void SaveUser() { }
    }

    public record User(string Name, string Email);

    public class Logger
    {
        public void Log(string message) { }
    }

    public record Settings(bool IsEnabled);
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(4);
            result.Should().Contain(c => c.ClassName == "UserService" && !c.IsRecord);
            result.Should().Contain(c => c.ClassName == "User" && c.IsRecord);
            result.Should().Contain(c => c.ClassName == "Logger" && !c.IsRecord);
            result.Should().Contain(c => c.ClassName == "Settings" && c.IsRecord);
        }

        [Fact]
        public async Task AnalyzeClassesAsync_RecordWithDocumentation_PreservesDocumentation()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    /// <summary>
    /// Represents a person
    /// </summary>
    public record Person
    {
        /// <summary>
        /// Gets the first name
        /// </summary>
        public string FirstName { get; init; }

        /// <summary>
        /// Gets the last name
        /// </summary>
        public string LastName { get; init; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);
            var interfaceCode = _service.GenerateInterface("IPerson", result[0], result[0].Members);

            // Assert
            interfaceCode.Should().Contain("/// <summary>");
            interfaceCode.Should().Contain("/// Gets the first name");
            interfaceCode.Should().Contain("/// Gets the last name");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalRecord_ExcludedByDefault()
        {
            // Arrange
            var sourceCode = @"
namespace Test
{
    public record PublicRecord(string Name);
    internal record InternalRecord(string Name);
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(1);
            result[0].ClassName.Should().Be("PublicRecord");
            result[0].Members.Should().HaveCount(1); // Name from primary constructor
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalRecord_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public record PublicRecord(string Name);
    internal record InternalRecord(string Name);
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(c => c.ClassName == "PublicRecord");
            result.Should().Contain(c => c.ClassName == "InternalRecord");
            // Each record should have one member (Name)
            result.ForEach(r => r.Members.Should().HaveCount(1));
        }

        private string CreateTempFile(string content)
        {
            var fileName = $"Test_{Guid.NewGuid()}.cs";
            var filePath = Path.Combine(_tempDirectory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }
    }
}