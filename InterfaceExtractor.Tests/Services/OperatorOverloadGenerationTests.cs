using FluentAssertions;
using InterfaceExtractor.Options;
using InterfaceExtractor.Services;
using Xunit;

namespace InterfaceExtractor.Tests.Services
{
    public class OperatorOverloadGenerationTests
    {
        [Fact]
        public void GenerateInterface_WithOperators_FormatsCorrectly()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeOperatorOverloads = true
            };
            var service = new InterfaceExtractorService(options);

            var classInfo = new ExtractedClassInfo
            {
                ClassName = "Vector",
                Namespace = "Math",
                Usings = new System.Collections.Generic.List<string> { "using System;" },
                Members = new System.Collections.Generic.List<MemberInfo>
                {
                    new MemberInfo
                    {
                        Type = MemberType.Property,
                        Signature = "double X { get; set; }",
                        Name = "X"
                    },
                    new MemberInfo
                    {
                        Type = MemberType.Operator,
                        Signature = "Vector operator +(Vector a, Vector b)",
                        Name = "operator +",
                        ReturnType = "Vector"
                    }
                }
            };

            // Act
            var result = service.GenerateInterface("IVector", classInfo, classInfo.Members);

            // Assert
            result.Should().Contain("namespace Math.Interfaces");
            result.Should().Contain("public interface IVector");
            result.Should().Contain("double X { get; set; }");
            result.Should().Contain("Vector operator +(Vector a, Vector b);");
        }

        [Fact]
        public void GenerateInterface_WithConversionOperators_FormatsCorrectly()
        {
            // Arrange
            var options = new ExtractorOptions
            {
                IncludeOperatorOverloads = true
            };
            var service = new InterfaceExtractorService(options);

            var classInfo = new ExtractedClassInfo
            {
                ClassName = "Money",
                Namespace = "Finance",
                Usings = new System.Collections.Generic.List<string>(),
                Members = new System.Collections.Generic.List<MemberInfo>
                {
                    new MemberInfo
                    {
                        Type = MemberType.Operator,
                        Signature = "implicit operator double(Money m)",
                        Name = "implicit operator double",
                        ReturnType = "double"
                    },
                    new MemberInfo
                    {
                        Type = MemberType.Operator,
                        Signature = "explicit operator string(Money m)",
                        Name = "explicit operator string",
                        ReturnType = "string"
                    }
                }
            };

            // Act
            var result = service.GenerateInterface("IMoney", classInfo, classInfo.Members);

            // Assert
            result.Should().Contain("implicit operator double(Money m);");
            result.Should().Contain("explicit operator string(Money m);");
        }
    }
}