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
    /// Tests for implementation stub generation (v1.2.0)
    /// </summary>
    public class ImplementationStubTests : IDisposable
    {
        private readonly string _tempDirectory;
        private bool _disposed;

        public ImplementationStubTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"StubTests_{Guid.NewGuid()}");
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
        public async Task GenerateImplementationStub_WithMethods_ThrowsNotImplementedException()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class UserService
    {
        public string GetUser(int id) { return null; }
        public void SaveUser(string name) { }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IUserService",
                "UserServiceImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public class UserServiceImplementation : IUserService");
            stubCode.Should().Contain("public string GetUser(int id)");
            stubCode.Should().Contain("throw new System.NotImplementedException();");
            stubCode.Should().Contain("public void SaveUser(string name)");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithProperties_GeneratesAutoProperties()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; }
        public string Email { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IPerson",
                "PersonImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public class PersonImplementation : IPerson");
            stubCode.Should().Contain("public string Name { get; set; }");
            stubCode.Should().Contain("public int Age { get; }");
            stubCode.Should().Contain("public string Email { get; set; }");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithEvents_GeneratesEventFields()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
using System;

namespace Test
{
    public class EventSource
    {
        public event EventHandler Started;
        public event EventHandler<DataEventArgs> DataReceived;
    }

    public class DataEventArgs : EventArgs { }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IEventSource",
                "EventSourceImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public class EventSourceImplementation : IEventSource");
            stubCode.Should().Contain("public event EventHandler Started;");
            stubCode.Should().Contain("public event EventHandler<DataEventArgs> DataReceived;");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithIndexers_GeneratesIndexerStubs()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class DataStore
    {
        public string this[int index] { get { return null; } set { } }
        public string this[string key] { get { return null; } }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IDataStore",
                "DataStoreImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public class DataStoreImplementation : IDataStore");
            // Stubs generate simplified indexer syntax
            stubCode.Should().Contain("public string this[int index] { get; set; }");
            stubCode.Should().Contain("public string this[string key] { get; }");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithTaskReturnType_ReturnsCompletedTask()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
using System.Threading.Tasks;

namespace Test
{
    public class AsyncService
    {
        public async Task ProcessAsync() { }
        public async Task<string> GetDataAsync() { return null; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IAsyncService",
                "AsyncServiceImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public Task ProcessAsync()");
            stubCode.Should().Contain("return Task.CompletedTask;");
            stubCode.Should().Contain("public Task<string> GetDataAsync()");
            stubCode.Should().Contain("throw new System.NotImplementedException();");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithDocumentation_PreservesDocumentation()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class Calculator
    {
        /// <summary>
        /// Adds two numbers
        /// </summary>
        /// <param name=""a"">First number</param>
        /// <param name=""b"">Second number</param>
        /// <returns>The sum</returns>
        public int Add(int a, int b) { return 0; }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "ICalculator",
                "CalculatorImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("/// <summary>");
            stubCode.Should().Contain("/// Adds two numbers");
            stubCode.Should().Contain("/// <param name=\"a\">First number</param>");
            stubCode.Should().Contain("/// <returns>The sum</returns>");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithGenericConstraints_IncludesConstraints()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class Repository
    {
        public T GetById<T>(int id) where T : class, new() { return null; }
        public void Save<T>(T entity) where T : class, IEntity { }
    }

    public interface IEntity { }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IRepository",
                "RepositoryImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public T GetById<T>(int id)");
            stubCode.Should().Contain("where T : class, new()");
            stubCode.Should().Contain("public void Save<T>(T entity)");
            stubCode.Should().Contain("where T : class, IEntity");
        }

        [Fact]
        public void GenerateImplementationStub_WithCustomSuffix_UsesCustomSuffix()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                ImplementationStubSuffix = "Service"
            };
            var service = new InterfaceExtractorService(options);

            var classInfo = new ExtractedClassInfo
            {
                ClassName = "User",
                Namespace = "Test",
                Usings = [],
                Members =
                [
                    new MemberInfo
                    {
                        Type = MemberType.Property,
                        Signature = "string Name { get; set; }",
                        Name = "Name"
                    }
                ]
            };

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IUser",
                "UserService",
                classInfo,
                classInfo.Members);

            // Assert
            stubCode.Should().Contain("public class UserService : IUser");
        }

        [Fact]
        public async Task GenerateImplementationStub_AddsUsingForInterfaceNamespace()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
using System;

namespace Test.Data
{
    public class Repository
    {
        public void Save() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IRepository",
                "RepositoryImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("using Test.Data.Interfaces;");
            stubCode.Should().Contain("namespace Test.Data");
        }

        [Fact]
        public async Task GenerateImplementationStub_WithFileHeader_IncludesHeader()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeFileHeader = true,
                FileHeaderTemplate = "// Auto-generated stub\n// Date: {Date}"
            };
            var service = new InterfaceExtractorService(options);

            var sourceCode = @"
namespace Test
{
    public class Service
    {
        public void Execute() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "IService",
                "ServiceImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().StartWith("// Auto-generated stub");
            stubCode.Should().Contain($"// Date: {DateTime.Now:yyyy-MM-dd}");
        }

        [Fact]
        public async Task GenerateImplementationStub_CompilesSuccessfully()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
using System;
using System.Threading.Tasks;

namespace Test
{
    public class CompleteService
    {
        public string Name { get; set; }
        public int Count { get; }

        public void DoSomething() { }
        public string GetValue() { return null; }
        public async Task ProcessAsync() { }
        public async Task<int> CalculateAsync() { return 0; }

        public event EventHandler Changed;

        public string this[int index] { get { return null; } set { } }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "ICompleteService",
                "CompleteServiceImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert - Can be parsed as valid C#
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(stubCode);
            var diagnostics = tree.GetDiagnostics();
            diagnostics.Should().NotContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }

        [Fact]
        public async Task GenerateImplementationStub_WithVoidMethod_HasEmptyBody()
        {
            // Arrange
            var service = new InterfaceExtractorService();

            var sourceCode = @"
namespace Test
{
    public class Logger
    {
        public void Log(string message) { }
        public void LogError(string message) { }
    }
}";
            var filePath = CreateTempFile(sourceCode);
            var classInfos = await service.AnalyzeClassesAsync(filePath);

            // Act
            var stubCode = service.GenerateImplementationStub(
                "ILogger",
                "LoggerImplementation",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            stubCode.Should().Contain("public class LoggerImplementation : ILogger");
            stubCode.Should().Contain("public void Log(string message)");
            stubCode.Should().Contain("public void LogError(string message)");

            // Void methods should have empty body (no return, no throw)
            // Check that the method body exists but is empty
            var lines = stubCode.Split('\n');
            var logMethodLine = Array.FindIndex(lines, l => l.Contains("public void Log(string message)"));
            logMethodLine.Should().BeGreaterThan(0);

            // The next line should be opening brace, followed by closing brace
            lines[logMethodLine + 1].Trim().Should().Be("{");
            lines[logMethodLine + 2].Trim().Should().Be("}");
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