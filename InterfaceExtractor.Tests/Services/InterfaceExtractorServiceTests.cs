using FluentAssertions;
using InterfaceExtractor.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace InterfaceExtractor.Tests.Services
{
    public class InterfaceExtractorServiceTests : IDisposable
    {
        private readonly InterfaceExtractorService _service;
        private readonly string _tempDirectory;
        private bool _disposed;

        public InterfaceExtractorServiceTests()
        {
            _service = new InterfaceExtractorService();
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"InterfaceExtractorTests_{Guid.NewGuid()}");
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

        #region AnalyzeClassesAsync Tests

        [Fact]
        public async Task AnalyzeClassesAsync_WithSimpleClass_ReturnsClassInfoAsync()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
        public void DoSomething() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].ClassName.Should().Be("TestClass");
            result[0].Namespace.Should().Be("TestNamespace");
            result[0].Members.Should().HaveCount(2);
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithMultipleClasses_ReturnsAllClassesAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class FirstClass
    {
        public string Name { get; set; }
    }

    public class SecondClass
    {
        public int Count { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().HaveCount(2);
            result.Select(c => c.ClassName).Should().Contain(new[] { "FirstClass", "SecondClass" });
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithNoPublicClasses_ReturnsEmptyListAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    internal class InternalClass
    {
        public string Name { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithClassWithNoPublicMembers_ReturnsEmptyListAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class EmptyClass
    {
        private string Name { get; set; }
        private void DoSomething() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task AnalyzeClassesAsync_WithInvalidFile_ThrowsInvalidOperationExceptionAsync()
        {
            // Arrange
            var filePath = Path.Combine(_tempDirectory, "nonexistent.cs");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AnalyzeClassesAsync(filePath));
        }

        #endregion AnalyzeClassesAsync Tests

        #region Member Extraction Tests

        [Fact]
        public async Task AnalyzeClassesAsync_ExtractsPublicMethodsAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void PublicMethod() { }
        private void PrivateMethod() { }
        public static void StaticMethod() { }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var members = result[0].Members;
            members.Should().HaveCount(1);
            members[0].Type.Should().Be(MemberType.Method);
            members[0].Name.Should().Be("PublicMethod");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_ExtractsPublicPropertiesAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string ReadWriteProperty { get; set; }
        public string ReadOnlyProperty { get; }
        public string WriteOnlyProperty { set { } }
        private string PrivateProperty { get; set; }
        public static string StaticProperty { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var members = result[0].Members;
            members.Should().HaveCount(3);
            members.Should().AllSatisfy(m => m.Type.Should().Be(MemberType.Property));
        }

        [Fact]
        public async Task AnalyzeClassesAsync_ExtractsPublicEventsAsync()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public event EventHandler Changed;
        private event EventHandler PrivateChanged;
        public static event EventHandler StaticChanged;
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var members = result[0].Members;
            members.Should().HaveCount(1);
            members[0].Type.Should().Be(MemberType.Event);
            members[0].Name.Should().Be("Changed");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_ExtractsPublicIndexersAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string this[int index] { get { return null; } set { } }
        private string this[string key] { get { return null; } }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var members = result[0].Members;
            members.Should().HaveCount(1);
            members[0].Type.Should().Be(MemberType.Indexer);
            members[0].Name.Should().Be("this[]");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_HandlesGenericMethodsWithConstraintsAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public T GetById<T>(int id) where T : class, new() { return null; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var member = result[0].Members[0];
            member.Signature.Should().Contain("<T>");
            member.Constraints.Should().Contain("where T : class, new()");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_PreservesXmlDocumentationAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var member = result[0].Members[0];
            member.Documentation.Should().NotBeNullOrEmpty();
            member.Documentation.Should().Contain("<summary>");
            member.Documentation.Should().Contain("Gets or sets the name.");
        }

        [Fact]
        public async Task AnalyzeClassesAsync_DetectsReadOnlyPropertiesAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string ReadOnly { get; }
        public string ReadWrite { get; set; }
        public string ExpressionBodied => ""value"";
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var members = result[0].Members;
            members[0].Signature.Should().Contain("{ get; }");
            members[1].Signature.Should().Contain("{ get; set; }");
            members[2].Signature.Should().Contain("{ get; }"); // Expression-bodied is read-only
        }

        [Fact]
        public async Task AnalyzeClassesAsync_HandlesPrivateSettersAsync()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; private set; }
    }
}";
            var filePath = CreateTempFile(sourceCode);

            // Act
            var result = await _service.AnalyzeClassesAsync(filePath);

            // Assert
            var member = result[0].Members[0];
            member.Signature.Should().Contain("{ get; }"); // Private setter should be excluded
        }

        #endregion Member Extraction Tests

        #region GenerateInterface Tests

        [Fact]
        public void GenerateInterface_WithBasicClass_GeneratesCorrectInterface()
        {
            // Arrange
            var classInfo = new ExtractedClassInfo
            {
                ClassName = "TestClass",
                Namespace = "TestNamespace",
                Usings = ["using System;"],
                Members =
                [
                    new MemberInfo
                    {
                        Type = MemberType.Property,
                        Signature = "string Name { get; set; }",
                        Name = "Name"
                    },
                    new MemberInfo
                    {
                        Type = MemberType.Method,
                        Signature = "void DoSomething()",
                        Name = "DoSomething"
                    }
                ]
            };

            // Act - using instance method
            var result = _service.GenerateInterface(
                "ITestClass",
                classInfo,
                classInfo.Members);

            // Assert
            result.Should().Contain("namespace TestNamespace.Interfaces");
            result.Should().Contain("public interface ITestClass");
            result.Should().Contain("string Name { get; set; }");
            result.Should().Contain("void DoSomething();");
            result.Should().Contain("using System;");
        }

        [Fact]
        public void GenerateInterface_WithXmlDocumentation_IncludesDocumentation()
        {
            // Arrange
            var classInfo = new ExtractedClassInfo
            {
                ClassName = "TestClass",
                Namespace = "TestNamespace",
                Usings = [],
                Members =
                [
                    new MemberInfo
                    {
                        Type = MemberType.Property,
                        Signature = "string Name { get; set; }",
                        Name = "Name",
                        Documentation = "/// <summary>\n/// Gets or sets the name.\n/// </summary>"
                    }
                ]
            };

            // Act
            var result = _service.GenerateInterface(
                "ITestClass",
                classInfo,
                classInfo.Members);

            // Assert
            result.Should().Contain("/// <summary>");
            result.Should().Contain("/// Gets or sets the name.");
            result.Should().Contain("/// </summary>");
        }

        [Fact]
        public void GenerateInterface_WithGenericConstraints_FormatsConstraintsCorrectly()
        {
            // Arrange
            var classInfo = new ExtractedClassInfo
            {
                ClassName = "TestClass",
                Namespace = "TestNamespace",
                Usings = [],
                Members =
                [
                    new MemberInfo
                    {
                        Type = MemberType.Method,
                        Signature = "T GetById<T>(int id)",
                        Constraints = "where T : class, new()",
                        Name = "GetById"
                    }
                ]
            };

            // Act
            var result = _service.GenerateInterface(
                "ITestClass",
                classInfo,
                classInfo.Members);

            // Assert
            result.Should().Contain("T GetById<T>(int id)");
            result.Should().Contain("where T : class, new();");
        }

        [Fact]
        public void GenerateInterface_WithMultipleMembers_SeparatesWithBlankLines()
        {
            // Arrange
            var classInfo = new ExtractedClassInfo
            {
                ClassName = "TestClass",
                Namespace = "TestNamespace",
                Usings = [],
                Members =
                [
                    new MemberInfo { Type = MemberType.Property, Signature = "string First { get; set; }", Name = "First" },
                    new MemberInfo { Type = MemberType.Property, Signature = "string Second { get; set; }", Name = "Second" }
                ]
            };

            // Act
            var result = _service.GenerateInterface(
                "ITestClass",
                classInfo,
                classInfo.Members);

            var lines = result.Split(['\r', '\n'], StringSplitOptions.None);
            var firstIndex = Array.FindIndex(lines, l => l.Contains("string First"));
            var secondIndex = Array.FindIndex(lines, l => l.Contains("string Second"));

            // There should be at least one empty line between members (index difference > 1)
            (secondIndex - firstIndex).Should().BeGreaterThan(1);

            // Verify there's actually an empty line between them
            var linesBetween = lines.Skip(firstIndex + 1).Take(secondIndex - firstIndex - 1);
            linesBetween.Should().Contain(line => string.IsNullOrWhiteSpace(line),
                "there should be a blank line between members");
        }

        #endregion GenerateInterface Tests

        #region AppendInterfaceToClass Tests

        [Fact]
        public void AppendInterfaceToClass_AddsInterfaceToClassWithNoBaseList()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act - using instance method
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert - Default options have AddUsingDirective=true, so uses simple name
            result.Should().Contain("public class TestClass : ITestClass");
            result.Should().Contain("using TestNamespace.Interfaces;");
        }

        [Fact]
        public void AppendInterfaceToClass_AddsInterfaceToClassWithExistingBaseClass()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass : BaseClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert - Default options have AddUsingDirective=true, so uses simple name
            result.Should().Contain("public class TestClass : BaseClass, ITestClass");
            result.Should().Contain("using TestNamespace.Interfaces;");
        }

        [Fact]
        public void AppendInterfaceToClass_DoesNotAddDuplicateInterface()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass : ITestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert
            result.Should().Be(sourceCode);
        }

        [Fact]
        public void AppendInterfaceToClass_UsesSameNamespaceWithoutFullyQualified()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace.Interfaces
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert
            result.Should().Contain("public class TestClass : ITestClass");
            result.Should().NotContain("TestNamespace.Interfaces.ITestClass");
        }

        [Fact]
        public void AppendInterfaceToClass_AddsUsingDirectiveWhenNeeded()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert
            result.Should().Contain("using TestNamespace.Interfaces;");
        }

        [Fact]
        public void AppendInterfaceToClass_WithAddUsingDirective_UsesSimpleName()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert - Should use simple name since using directive is added
            result.Should().Contain("public class TestClass : ITestClass");
            result.Should().NotContain("TestNamespace.Interfaces.ITestClass");
            result.Should().Contain("using TestNamespace.Interfaces;");
        }

        [Fact]
        public void AppendInterfaceToClass_DoesNotAddDuplicateUsing()
        {
            // Arrange
            var sourceCode = @"
using System;
using TestNamespace.Interfaces;

namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert
            var usingCount = result.Split(["using TestNamespace.Interfaces;"], StringSplitOptions.None).Length - 1;
            usingCount.Should().Be(1);
        }

        [Fact]
        public void AppendInterfaceToClass_ReturnsOriginalWhenClassNotFound()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class OtherClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = _service.AppendInterfaceToClass(
                sourceCode,
                "TestClass",
                "ITestClass",
                "TestNamespace.Interfaces");

            // Assert
            result.Should().Be(sourceCode);
        }

        #endregion AppendInterfaceToClass Tests

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