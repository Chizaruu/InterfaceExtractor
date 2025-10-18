using FluentAssertions;
using InterfaceExtractor.Services;
using InterfaceExtractor.Tests.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace InterfaceExtractor.Tests.Integration
{
    /// <summary>
    /// Integration tests that test the complete flow from analysis to interface generation
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly InterfaceExtractorService _service;
        private readonly string _tempDirectory;
        private bool _disposed;

        public IntegrationTests()
        {
            _service = new InterfaceExtractorService();
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"IntegrationTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && Directory.Exists(_tempDirectory))
                {
                    try
                    {
                        Directory.Delete(_tempDirectory, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors in tests
                    }
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
        public async Task CompleteFlow_SimpleClass_GeneratesCorrectInterfaceAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.SimpleClass,
                _tempDirectory);

            // Act - Analyze
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Assert - Analysis
            classInfos.Should().HaveCount(1);
            classInfos[0].ClassName.Should().Be("SimpleClass");
            classInfos[0].Namespace.Should().Be("TestNamespace");
            classInfos[0].Members.Should().HaveCount(3); // Name, Age, DoSomething

            // Act - Generate
            var interfaceCode = InterfaceExtractorService.GenerateInterface(
                "ISimpleClass",
                classInfos[0],
                classInfos[0].Members);

            // Assert - Generated Interface
            interfaceCode.Should().Contain("public interface ISimpleClass");
            interfaceCode.Should().Contain("string Name { get; set; }");
            interfaceCode.Should().Contain("int Age { get; set; }");
            interfaceCode.Should().Contain("void DoSomething();");
            interfaceCode.Should().Contain("namespace TestNamespace.Interfaces");
        }

        [Fact]
        public async Task CompleteFlow_ClassWithDocumentation_PreservesDocumentationAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.ClassWithDocumentation,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);
            var interfaceCode = InterfaceExtractorService.GenerateInterface(
                "IDocumentedClass",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            interfaceCode.Should().Contain("/// <summary>");
            interfaceCode.Should().Contain("/// Gets or sets the name");
            interfaceCode.Should().Contain("/// Performs an action");
            interfaceCode.Should().Contain("/// <param name=\"value\">");
            interfaceCode.Should().Contain("/// <returns>");
        }

        [Fact]
        public async Task CompleteFlow_GenericClass_HandlesConstraintsAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.GenericClass,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);
            var interfaceCode = InterfaceExtractorService.GenerateInterface(
                "IGenericRepository",
                classInfos[0],
                classInfos[0].Members);

            // Assert
            interfaceCode.Should().Contain("T GetById<T>(int id)");
            interfaceCode.Should().Contain("where T : class, new();");
            interfaceCode.Should().Contain("List<T> GetAll<T>()");
            interfaceCode.Should().Contain("where T : IEntity;");
            interfaceCode.Should().Contain("void Update<T>(T entity)");
            interfaceCode.Should().Contain("where T : class, IEntity;");
        }

        [Fact]
        public async Task CompleteFlow_MultipleMemberTypes_ExtractsAllCorrectlyAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.ClassWithMultipleMemberTypes,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);
            var members = classInfos[0].Members;

            // Assert - Should have properties, methods, events, and indexers
            members.Should().Contain(m => m.Type == MemberType.Property && m.Name == "Name");
            members.Should().Contain(m => m.Type == MemberType.Property && m.Name == "Count");
            members.Should().Contain(m => m.Type == MemberType.Method && m.Name == "DoSomething");
            members.Should().Contain(m => m.Type == MemberType.Method && m.Name == "GetValue");
            members.Should().Contain(m => m.Type == MemberType.Event && m.Name == "Changed");
            members.Should().Contain(m => m.Type == MemberType.Event && m.Name == "DataChanged");
            members.Should().Contain(m => m.Type == MemberType.Indexer && m.Name == "this[]");

            // Should NOT have private or static members
            members.Should().NotContain(m => m.Name == "PrivateProperty");
            members.Should().NotContain(m => m.Name == "PrivateMethod");
            members.Should().NotContain(m => m.Name == "StaticProperty");
            members.Should().NotContain(m => m.Name == "StaticMethod");
        }

        [Fact]
        public async Task CompleteFlow_MultipleClasses_ProcessesAllAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.MultipleClasses,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            classInfos.Should().HaveCount(2); // Only public classes
            classInfos.Should().Contain(c => c.ClassName == "FirstClass");
            classInfos.Should().Contain(c => c.ClassName == "SecondClass");
            classInfos.Should().NotContain(c => c.ClassName == "InternalClass");
        }

        [Fact]
        public async Task CompleteFlow_ReadOnlyProperties_DetectsCorrectlyAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.ClassWithReadOnlyProperties,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);
            var members = classInfos[0].Members;

            // Assert
            var readOnly = members.First(m => m.Name == "ReadOnly");
            readOnly.Signature.Should().Contain("{ get; }");
            readOnly.Signature.Should().NotContain("set");

            var readWrite = members.First(m => m.Name == "ReadWrite");
            readWrite.Signature.Should().Contain("{ get; set; }");

            var expressionBodied = members.First(m => m.Name == "ExpressionBodied");
            expressionBodied.Signature.Should().Contain("{ get; }");

            var privateSetter = members.First(m => m.Name == "PropertyWithPrivateSetter");
            privateSetter.Signature.Should().Contain("{ get; }");
            privateSetter.Signature.Should().NotContain("set");
        }

        [Fact]
        public void CompleteFlow_AppendInterface_UpdatesClassAsync()
        {
            // Arrange
            var sourceCode = TestHelpers.SampleCode.SimpleClass;

            // Act - Append interface to class
            var updatedCode = InterfaceExtractorService.AppendInterfaceToClass(
                sourceCode,
                "SimpleClass",
                "ISimpleClass",
                "TestNamespace.Interfaces");

            // Assert
            updatedCode.Should().Contain("public class SimpleClass : TestNamespace.Interfaces.ISimpleClass");
            updatedCode.Should().Contain("using TestNamespace.Interfaces;");
        }

        [Fact]
        public async Task CompleteFlow_SelectiveMembers_GeneratesPartialInterfaceAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.SimpleClass,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Select only the Name property
            var selectedMembers = classInfos[0].Members
                .Where(m => m.Name == "Name")
                .ToList();

            var interfaceCode = InterfaceExtractorService.GenerateInterface(
                "ISimpleClass",
                classInfos[0],
                selectedMembers);

            // Assert
            interfaceCode.Should().Contain("string Name { get; set; }");
            interfaceCode.Should().NotContain("int Age");
            interfaceCode.Should().NotContain("void DoSomething");
        }

        [Fact]
        public async Task CompleteFlow_EmptyClass_ReturnsNoClassInfoAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.EmptyClass,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            classInfos.Should().BeEmpty();
        }

        [Fact]
        public async Task CompleteFlow_ClassWithOnlyPrivateMembers_ReturnsNoClassInfoAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.ClassWithOnlyPrivateMembers,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            classInfos.Should().BeEmpty();
        }

        [Fact]
        public async Task CompleteFlow_FileScopedNamespace_WorksAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.FileScopedNamespaceClass,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            classInfos.Should().HaveCount(1);
            classInfos[0].ClassName.Should().Be("FileScopedClass");
            classInfos[0].Namespace.Should().Be("TestNamespace");
        }

        [Fact]
        public async Task CompleteFlow_SaveToFile_CreatesValidCSharpFileAsync()
        {
            // Arrange
            var filePath = TestHelpers.CreateTempCSharpFile(
                TestHelpers.SampleCode.SimpleClass,
                _tempDirectory);

            // Act
            var classInfos = await _service.AnalyzeClassesAsync(filePath);
            var interfaceCode = InterfaceExtractorService.GenerateInterface(
                "ISimpleClass",
                classInfos[0],
                classInfos[0].Members);

            // Save to file
            var interfacePath = Path.Combine(_tempDirectory, "ISimpleClass.cs");
            File.WriteAllText(interfacePath, interfaceCode);

            // Assert - File exists
            File.Exists(interfacePath).Should().BeTrue();

            // Assert - Can parse as valid C#
            var savedContent = File.ReadAllText(interfacePath);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(savedContent);
            var diagnostics = tree.GetDiagnostics();

            diagnostics.Should().NotContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }
}