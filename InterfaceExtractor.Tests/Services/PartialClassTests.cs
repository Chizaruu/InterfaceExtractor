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
    /// Tests for partial class support (v1.2.0)
    /// </summary>
    public class PartialClassTests : IDisposable
    {
        private readonly string _tempDirectory;
        private bool _disposed;

        public PartialClassTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"PartialTests_{Guid.NewGuid()}");
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
        public async Task AnalyzeClassesAsync_SinglePartialFile_DetectsPartialModifier()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public partial class UserService
    {
        public void GetUser() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath, _tempDirectory);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsPartial.Should().BeTrue();
            result[0].Members.Should().HaveCount(1);
        }

        [Fact]
        public async Task AnalyzeClassesAsync_MultiplePartialFiles_CombinesMembers()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test
{
    public partial class UserService
    {
        public void GetUser(int id) { }
        public void UpdateUser() { }
    }
}";
            var file2Code = @"
namespace Test
{
    public partial class UserService
    {
        public void DeleteUser(int id) { }
        public void ListUsers() { }
    }
}";
            CreateTempFile(file1Code, "UserService.Part1.cs");
            var file2Path = CreateTempFile(file2Code, "UserService.Part2.cs");

            // Act - analyze from file2
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsPartial.Should().BeTrue();
            result[0].Members.Should().HaveCount(4); // All 4 methods combined
            result[0].Members.Should().Contain(m => m.Name == "GetUser");
            result[0].Members.Should().Contain(m => m.Name == "UpdateUser");
            result[0].Members.Should().Contain(m => m.Name == "DeleteUser");
            result[0].Members.Should().Contain(m => m.Name == "ListUsers");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialClassesInDifferentNamespaces_DoesNotCombine()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test.Services
{
    public partial class UserService
    {
        public void GetUser() { }
    }
}";
            var file2Code = @"
namespace Test.Data
{
    public partial class UserService
    {
        public void SaveUser() { }
    }
}";
            CreateTempFile(file1Code, "UserService1.cs");
            var file2Path = CreateTempFile(file2Code, "UserService2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result.Should().HaveCount(1);
            result[0].Namespace.Should().Be("Test.Data");
            result[0].Members.Should().HaveCount(1); // Only SaveUser
            result[0].Members.Should().NotContain(m => m.Name == "GetUser");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialWithDuplicateSignatures_NoDuplicates()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test
{
    public partial class DataService
    {
        public string Name { get; set; }
        public void Process() { }
    }
}";
            var file2Code = @"
namespace Test
{
    public partial class DataService
    {
        public string Name { get; set; }  // Duplicate
        public void Execute() { }
    }
}";
            CreateTempFile(file1Code, "DataService1.cs");
            var file2Path = CreateTempFile(file2Code, "DataService2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result[0].Members.Should().HaveCount(3); // Name (once), Process, Execute
            result[0].Members.Count(m => m.Name == "Name").Should().Be(1);
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialAnalysisDisabled_OnlyCurrentFile()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = false };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test
{
    public partial class UserService
    {
        public void GetUser() { }
    }
}";
            var file2Code = @"
namespace Test
{
    public partial class UserService
    {
        public void DeleteUser() { }
    }
}";
            CreateTempFile(file1Code, "UserService1.cs");
            var file2Path = CreateTempFile(file2Code, "UserService2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result.Should().HaveCount(1);
            result[0].Members.Should().HaveCount(1); // Only DeleteUser from current file
            result[0].Members.Should().Contain(m => m.Name == "DeleteUser");
            result[0].Members.Should().NotContain(m => m.Name == "GetUser");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialWithProperties_CombinesAll()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test
{
    public partial class Person
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}";
            var file2Code = @"
namespace Test
{
    public partial class Person
    {
        public int Age { get; set; }
        public string Email { get; set; }
    }
}";
            CreateTempFile(file1Code, "Person1.cs");
            var file2Path = CreateTempFile(file2Code, "Person2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result[0].Members.Should().HaveCount(4);
            result[0].Members.Should().Contain(m => m.Name == "FirstName");
            result[0].Members.Should().Contain(m => m.Name == "LastName");
            result[0].Members.Should().Contain(m => m.Name == "Age");
            result[0].Members.Should().Contain(m => m.Name == "Email");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialWithEvents_CombinesAll()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
using System;

namespace Test
{
    public partial class EventManager
    {
        public event EventHandler Started;
        public void Start() { }
    }
}";
            var file2Code = @"
using System;

namespace Test
{
    public partial class EventManager
    {
        public event EventHandler Stopped;
        public void Stop() { }
    }
}";
            CreateTempFile(file1Code, "EventManager1.cs");
            var file2Path = CreateTempFile(file2Code, "EventManager2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result[0].Members.Should().HaveCount(4); // 2 events + 2 methods
            result[0].Members.Should().Contain(m => m.Type == MemberType.Event && m.Name == "Started");
            result[0].Members.Should().Contain(m => m.Type == MemberType.Event && m.Name == "Stopped");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialRecordNotSupported_OnlyAnalyzesCurrentFile()
        {
            // Arrange - partial records exist but are uncommon
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test
{
    public partial record UserRecord
    {
        public string Name { get; init; }
    }
}";
            var file2Code = @"
namespace Test
{
    public partial record UserRecord
    {
        public int Age { get; init; }
    }
}";
            CreateTempFile(file1Code, "UserRecord1.cs");
            var file2Path = CreateTempFile(file2Code, "UserRecord2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsRecord.Should().BeTrue();
            result[0].IsPartial.Should().BeTrue();
            // Should combine members from both files
            result[0].Members.Should().HaveCount(2);
        }

        [Fact]
        public async Task AnalyzeClassesAsync_NonPartialClass_IgnoresOtherFiles()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var file1Code = @"
namespace Test
{
    public class UserService
    {
        public void GetUser() { }
    }
}";
            var file2Code = @"
namespace Test
{
    public class UserService  // Same name but not partial
    {
        public void DeleteUser() { }
    }
}";
            CreateTempFile(file1Code, "UserService1.cs");
            var file2Path = CreateTempFile(file2Code, "UserService2.cs");

            // Act
            var result = await service.AnalyzeClassesAsync(file2Path, _tempDirectory);

            // Assert
            result.Should().HaveCount(1);
            result[0].IsPartial.Should().BeFalse();
            result[0].Members.Should().HaveCount(1); // Only DeleteUser from current file
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PartialWithInvalidFiles_SkipsInvalidFiles()
        {
            // Arrange
            var options = new ExtractorOptions { AnalyzePartialClasses = true };
            var service = new InterfaceExtractorService(options);

            var validCode = @"
namespace Test
{
    public partial class DataService
    {
        public void GetData() { }
    }
}";
            // This is truly invalid - completely malformed syntax
            var invalidCode = @"
namespace Test
{
    public partial class DataService @#$%^&*()
        public void SaveData() { }
    }
}";
            CreateTempFile(invalidCode, "DataService1.cs"); // Invalid file
            var validFilePath = CreateTempFile(validCode, "DataService2.cs");

            // Act - should not throw, just skip invalid files
            var result = await service.AnalyzeClassesAsync(validFilePath, _tempDirectory);

            // Assert - Only GetData from the valid file should be included
            result.Should().HaveCount(1);
            result[0].Members.Should().HaveCount(1);
            result[0].Members.Should().Contain(m => m.Name == "GetData");
            result[0].Members.Should().NotContain(m => m.Name == "SaveData");
        }

        private string CreateTempFile(string content, string fileName = null)
        {
            fileName ??= $"Test_{Guid.NewGuid()}.cs";
            var filePath = Path.Combine(_tempDirectory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }
    }
}