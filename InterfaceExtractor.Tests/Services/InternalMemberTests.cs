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
    /// Tests for internal member support (v1.2.0)
    /// </summary>
    public class InternalMemberTests : IDisposable
    {
        private readonly string _tempDirectory;
        private bool _disposed;

        public InternalMemberTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"InternalTests_{Guid.NewGuid()}");
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
        public async Task AnalyzeClassesAsync_InternalMembers_ExcludedByDefault()
        {
            // Arrange
            var service = new InterfaceExtractorService(); // Default: IncludeInternalMembers = false

            var sourceCode = @"
namespace Test
{
    public class DataService
    {
        public void PublicMethod() { }
        internal void InternalMethod() { }
        private void PrivateMethod() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result[0].Members.Should().HaveCount(1);
            result[0].Members.Should().Contain(m => m.Name == "PublicMethod");
            result[0].Members.Should().NotContain(m => m.Name == "InternalMethod");
            result[0].Members.Should().NotContain(m => m.Name == "PrivateMethod");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalMembers_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class DataService
    {
        public void PublicMethod() { }
        internal void InternalMethod() { }
        private void PrivateMethod() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result[0].Members.Should().HaveCount(2);
            result[0].Members.Should().Contain(m => m.Name == "PublicMethod");
            result[0].Members.Should().Contain(m => m.Name == "InternalMethod");
            result[0].Members.Should().NotContain(m => m.Name == "PrivateMethod");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalProperties_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class Config
    {
        public string PublicSetting { get; set; }
        internal string InternalSetting { get; set; }
        private string PrivateSetting { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result[0].Members.Should().HaveCount(2);
            result[0].Members.Should().Contain(m => m.Name == "PublicSetting");
            result[0].Members.Should().Contain(m => m.Name == "InternalSetting");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalEvents_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
using System;

namespace Test
{
    public class EventManager
    {
        public event EventHandler PublicEvent;
        internal event EventHandler InternalEvent;
        private event EventHandler PrivateEvent;
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result[0].Members.Should().HaveCount(2);
            result[0].Members.Should().Contain(m => m.Name == "PublicEvent");
            result[0].Members.Should().Contain(m => m.Name == "InternalEvent");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalIndexers_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class DataStore
    {
        public string this[int index]
        {
            get { return null; }
            set { }
        }

        internal string this[string key]
        {
            get { return null; }
        }

        private string this[long id]
        {
            get { return null; }
        }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result[0].Members.Should().HaveCount(2);
            result[0].Members.Count(m => m.Type == MemberType.Indexer).Should().Be(2);
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalClass_ExcludedByDefault()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class PublicClass
    {
        public void Method() { }
    }

    internal class InternalClass
    {
        public void Method() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(1);
            result[0].ClassName.Should().Be("PublicClass");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalClass_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class PublicClass
    {
        public void Method() { }
    }

    internal class InternalClass
    {
        public void Method() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(c => c.ClassName == "PublicClass");
            result.Should().Contain(c => c.ClassName == "InternalClass");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_ImplicitInternalClass_IncludedWithOption()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    class ImplicitInternalClass  // No access modifier = internal
    {
        public void Method() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(1);
            result[0].ClassName.Should().Be("ImplicitInternalClass");
        }

        [Fact]
        public async Task GenerateInterface_WithInternalMembers_CreatesValidInterface()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class ApiService
    {
        public void PublicApi() { }
        internal void InternalApi() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var interfaceCode = service.GenerateInterface(
                "IApiService",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            interfaceCode.Should().Contain("public interface IApiService");
            interfaceCode.Should().Contain("void PublicApi();");
            interfaceCode.Should().Contain("void InternalApi();");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_MixedAccessLevels_FiltersCorrectly()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class MultiAccessService
    {
        public void PublicMethod() { }
        internal void InternalMethod() { }
        protected void ProtectedMethod() { }
        protected internal void ProtectedInternalMethod() { }
        private void PrivateMethod() { }
        private protected void PrivateProtectedMethod() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            // Should include: public, internal, protected internal
            // Should exclude: protected, private, private protected
            result[0].Members.Should().HaveCountGreaterThanOrEqualTo(2);
            result[0].Members.Should().Contain(m => m.Name == "PublicMethod");
            result[0].Members.Should().Contain(m => m.Name == "InternalMethod");
            result[0].Members.Should().NotContain(m => m.Name == "PrivateMethod");
            result[0].Members.Should().NotContain(m => m.Name == "ProtectedMethod");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_InternalStaticMembers_StillExcluded()
        {
            // Arrange
            var options = new ExtractorOptions { IncludeInternalMembers = true };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class Utilities
    {
        public static void PublicStaticMethod() { }
        internal static void InternalStaticMethod() { }
        public void PublicInstanceMethod() { }
        internal void InternalInstanceMethod() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await service.AnalyzeClassesAsync(filePath);

            // Assert
            // Static members should be excluded even if public or internal
            result[0].Members.Should().HaveCount(2);
            result[0].Members.Should().Contain(m => m.Name == "PublicInstanceMethod");
            result[0].Members.Should().Contain(m => m.Name == "InternalInstanceMethod");
            result[0].Members.Should().NotContain(m => m.Name == "PublicStaticMethod");
            result[0].Members.Should().NotContain(m => m.Name == "InternalStaticMethod");
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