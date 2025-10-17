using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterfaceExtractor.Services
{
    public class InterfaceExtractorService
    {
        public async Task<ExtractedClassInfo> AnalyzeClassAsync(string filePath)
        {
            return await Task.Run(() => AnalyzeClass(filePath));
        }

        private ExtractedClassInfo AnalyzeClass(string filePath)
        {
            try
            {
                var sourceCode = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = tree.GetRoot();

                // Find the first public class
                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));

                if (classDeclaration == null)
                {
                    return null;
                }

                var className = classDeclaration.Identifier.Text;

                // Extract namespace
                var namespaceDeclaration = classDeclaration.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                var namespaceName = namespaceDeclaration?.Name.ToString() ?? "DefaultNamespace";

                // Extract public methods and properties
                var members = ExtractPublicMembers(classDeclaration);

                // Extract using statements
                var usings = root.DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Select(u => u.ToString())
                    .Distinct()
                    .ToList();

                return new ExtractedClassInfo
                {
                    ClassName = className,
                    Namespace = namespaceName,
                    Members = members,
                    Usings = usings,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze class: {ex.Message}", ex);
            }
        }

        public static string GenerateInterface(string interfaceName, ExtractedClassInfo classInfo, List<MemberInfo> selectedMembers)
        {
            var sb = new StringBuilder();

            // Add usings
            foreach (var usingDirective in classInfo.Usings)
            {
                sb.AppendLine(usingDirective);
            }

            if (classInfo.Usings.Any())
            {
                sb.AppendLine();
            }

            // Start namespace
            sb.AppendLine($"namespace {classInfo.Namespace}.Interfaces");
            sb.AppendLine("{");

            // Start interface
            sb.AppendLine($"    public interface {interfaceName}");
            sb.AppendLine("    {");

            // Add selected members
            foreach (var member in selectedMembers)
            {
                if (!string.IsNullOrWhiteSpace(member.Constraints))
                {
                    sb.AppendLine($"        {member.Signature}");
                    sb.AppendLine($"            {member.Constraints};");
                }
                else
                {
                    sb.AppendLine($"        {member.Signature};");
                }
            }

            // Close interface and namespace
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private List<MemberInfo> ExtractPublicMembers(ClassDeclarationSyntax classDeclaration)
        {
            var members = new List<MemberInfo>();

            // Extract public methods
            var methods = classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                .Where(m => !m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var method in methods)
            {
                var returnType = method.ReturnType.ToString();
                var methodName = method.Identifier.Text;
                var parameters = method.ParameterList.ToString();
                var typeParameters = method.TypeParameterList?.ToString() ?? "";
                var constraints = string.Join(" ", method.ConstraintClauses.Select(c => c.ToString()));

                members.Add(new MemberInfo
                {
                    Type = MemberType.Method,
                    Signature = $"{returnType} {methodName}{typeParameters}{parameters}",
                    Constraints = constraints,
                    Name = methodName,
                    ReturnType = returnType
                });
            }

            // Extract public properties
            var properties = classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                .Where(p => !p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var property in properties)
            {
                var propType = property.Type.ToString();
                var propName = property.Identifier.Text;
                var accessors = new List<string>();

                if (property.AccessorList != null)
                {
                    foreach (var accessor in property.AccessorList.Accessors)
                    {
                        accessors.Add(accessor.Keyword.Text);
                    }
                }

                var accessorList = accessors.Any() ? $" {{ {string.Join("; ", accessors)}; }}" : " { get; set; }";

                members.Add(new MemberInfo
                {
                    Type = MemberType.Property,
                    Signature = $"{propType} {propName}{accessorList}",
                    Name = propName,
                    ReturnType = propType
                });
            }

            return members;
        }
    }

    public class ExtractedClassInfo
    {
        public string ClassName { get; set; }
        public string Namespace { get; set; }
        public List<MemberInfo> Members { get; set; }
        public List<string> Usings { get; set; }
        public string FilePath { get; set; }
    }

    public class MemberInfo
    {
        public MemberType Type { get; set; }
        public string Signature { get; set; }
        public string Constraints { get; set; }
        public string Name { get; set; }
        public string ReturnType { get; set; }
    }

    public enum MemberType
    {
        Method,
        Property
    }
}