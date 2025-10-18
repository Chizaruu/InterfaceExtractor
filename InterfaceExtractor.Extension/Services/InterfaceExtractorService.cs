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
        public async Task<List<ExtractedClassInfo>> AnalyzeClassesAsync(string filePath)
        {
            return await Task.Run(() => AnalyzeClasses(filePath));
        }

        private List<ExtractedClassInfo> AnalyzeClasses(string filePath)
        {
            try
            {
                var sourceCode = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = tree.GetRoot();

                // Find all public classes
                var classDeclarations = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                    .ToList();

                if (!classDeclarations.Any())
                {
                    return new List<ExtractedClassInfo>(); // Return empty collection instead of null
                }

                var results = new List<ExtractedClassInfo>();

                // Extract using statements once
                var usings = root.DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Select(u => u.ToString())
                    .Distinct()
                    .ToList();

                foreach (var classDeclaration in classDeclarations)
                {
                    var className = classDeclaration.Identifier.Text;

                    // Extract namespace - simplified condition
                    var namespaceDeclaration = classDeclaration.Ancestors()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .FirstOrDefault();

                    var namespaceName = namespaceDeclaration?.Name.ToString() ?? "DefaultNamespace";

                    // Extract public members
                    var members = ExtractPublicMembers(classDeclaration);

                    if (members.Any())
                    {
                        results.Add(new ExtractedClassInfo
                        {
                            ClassName = className,
                            Namespace = namespaceName,
                            Members = members,
                            Usings = usings,
                            FilePath = filePath
                        });
                    }
                }

                return results;
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
            sb.AppendLine($"namespace {classInfo.Namespace}{Constants.InterfacesNamespaceSuffix}");
            sb.AppendLine("{");

            // Start interface
            sb.AppendLine($"    public interface {interfaceName}");
            sb.AppendLine("    {");

            // Add selected members
            for (int i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];

                // Add blank line between members except for first one
                if (i > 0)
                {
                    sb.AppendLine();
                }

                // Add XML documentation comment if available
                if (!string.IsNullOrWhiteSpace(member.Documentation))
                {
                    foreach (var line in member.Documentation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            // Ensure the line starts with ///
                            if (trimmedLine.StartsWith("///"))
                            {
                                sb.AppendLine($"        {trimmedLine}");
                            }
                            else
                            {
                                sb.AppendLine($"        /// {trimmedLine}");
                            }
                        }
                    }
                }

                // Add member signature
                if (!string.IsNullOrWhiteSpace(member.Constraints))
                {
                    sb.AppendLine($"        {member.Signature}");
                    sb.AppendLine($"            {member.Constraints};");
                }
                else
                {
                    // Only add semicolon if the signature doesn't end with }
                    // Properties/indexers with accessor blocks end with }, methods/events don't
                    var needsSemicolon = !member.Signature.TrimEnd().EndsWith("}");

                    if (needsSemicolon)
                    {
                        sb.AppendLine($"        {member.Signature};");
                    }
                    else
                    {
                        sb.AppendLine($"        {member.Signature}");
                    }
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

            // Extract public methods - simplified with Where clause
            var methods = classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                           !m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var method in methods)
            {
                var returnType = method.ReturnType.ToString();
                var methodName = method.Identifier.Text;
                var parameters = method.ParameterList.ToString();
                var typeParameters = method.TypeParameterList?.ToString() ?? "";
                var constraints = string.Join(" ", method.ConstraintClauses.Select(c => c.ToString()));
                var documentation = ExtractDocumentation(method);

                members.Add(new MemberInfo
                {
                    Type = MemberType.Method,
                    Signature = $"{returnType} {methodName}{typeParameters}{parameters}",
                    Constraints = constraints,
                    Name = methodName,
                    ReturnType = returnType,
                    Documentation = documentation
                });
            }

            // Extract public properties - simplified with Where clause
            var properties = classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                           !p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var property in properties)
            {
                var propType = property.Type.ToString();
                var propName = property.Identifier.Text;
                var documentation = ExtractDocumentation(property);

                // Simplified accessor logic using LINQ Where
                var accessors = new List<string>();
                if (property.AccessorList != null)
                {
                    accessors = property.AccessorList.Accessors
                        .Where(accessor => !accessor.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                        .Select(accessor => accessor.Keyword.Text)
                        .ToList();
                }
                else if (property.ExpressionBody != null)
                {
                    // Expression-bodied property (read-only)
                    accessors.Add("get");
                }

                var accessorList = accessors.Any()
                    ? $" {{ {string.Join("; ", accessors)}; }}"
                    : " { get; }"; // Default to read-only

                members.Add(new MemberInfo
                {
                    Type = MemberType.Property,
                    Signature = $"{propType} {propName}{accessorList}",
                    Name = propName,
                    ReturnType = propType,
                    Documentation = documentation
                });
            }

            // Extract public events - simplified with Where clause
            var events = classDeclaration.Members
                .OfType<EventFieldDeclarationSyntax>()
                .Where(e => e.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                           !e.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var eventField in events)
            {
                var eventType = eventField.Declaration.Type.ToString();
                var documentation = ExtractDocumentation(eventField);

                foreach (var variable in eventField.Declaration.Variables)
                {
                    var eventName = variable.Identifier.Text;

                    members.Add(new MemberInfo
                    {
                        Type = MemberType.Event,
                        Signature = $"event {eventType} {eventName}",
                        Name = eventName,
                        ReturnType = eventType,
                        Documentation = documentation
                    });
                }
            }

            // Extract public indexers - simplified with Where clause
            var indexers = classDeclaration.Members
                .OfType<IndexerDeclarationSyntax>()
                .Where(i => i.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                           !i.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var indexer in indexers)
            {
                var indexerType = indexer.Type.ToString();
                var parameters = indexer.ParameterList.ToString();
                var documentation = ExtractDocumentation(indexer);

                // Simplified accessor logic using LINQ Where
                var accessors = new List<string>();
                if (indexer.AccessorList != null)
                {
                    accessors = indexer.AccessorList.Accessors
                        .Where(accessor => !accessor.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                        .Select(accessor => accessor.Keyword.Text)
                        .ToList();
                }

                var accessorList = accessors.Any()
                    ? $" {{ {string.Join("; ", accessors)}; }}"
                    : " { get; }";

                members.Add(new MemberInfo
                {
                    Type = MemberType.Indexer,
                    Signature = $"{indexerType} this{parameters}{accessorList}",
                    Name = "this[]",
                    ReturnType = indexerType,
                    Documentation = documentation
                });
            }

            return members;
        }

        private static string ExtractDocumentation(MemberDeclarationSyntax member)
        {
            var trivia = member.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (trivia != default)
            {
                return trivia.ToString().Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Appends the interface to the class declaration
        /// </summary>
        public static string AppendInterfaceToClass(string sourceCode, string className, string interfaceName, string interfaceNamespace)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Find the target class
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
            {
                return sourceCode; // Class not found, return original
            }

            // Get the class's namespace
            var classNamespace = classDeclaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            var classNamespaceName = classNamespace?.Name.ToString() ?? "";

            // Determine if we need to use fully qualified name
            string interfaceToAdd;
            if (classNamespaceName == interfaceNamespace ||
                string.IsNullOrEmpty(interfaceNamespace))
            {
                // Same namespace, use simple name
                interfaceToAdd = interfaceName;
            }
            else
            {
                // Different namespace, use fully qualified name
                interfaceToAdd = $"{interfaceNamespace}.{interfaceName}";
            }

            // Check if interface is already implemented
            if (classDeclaration.BaseList != null)
            {
                var existingBases = classDeclaration.BaseList.Types
                    .Select(t => t.ToString())
                    .ToList();

                if (existingBases.Any(b => b.Contains(interfaceName)))
                {
                    return sourceCode; // Already implements this interface
                }
            }

            // Add the interface to the base list
            ClassDeclarationSyntax newClassDeclaration;

            if (classDeclaration.BaseList == null)
            {
                // No base list, create one
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(interfaceToAdd));

                var baseList = SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType));

                newClassDeclaration = classDeclaration.WithBaseList(baseList);
            }
            else
            {
                // Add to existing base list
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(interfaceToAdd));

                var newBaseList = classDeclaration.BaseList.AddTypes(baseType);
                newClassDeclaration = classDeclaration.WithBaseList(newBaseList);
            }

            // Replace the old class with the new one
            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            // Add using directive if needed (for different namespace)
            if (classNamespaceName != interfaceNamespace &&
                !string.IsNullOrEmpty(interfaceNamespace))
            {
                var usingDirective = SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(interfaceNamespace));

                // Check if using already exists
                var existingUsings = newRoot.Usings
                    .Select(u => u.Name.ToString())
                    .ToList();

                if (!existingUsings.Contains(interfaceNamespace))
                {
                    newRoot = newRoot.AddUsings(usingDirective);
                }
            }

            // Add NormalizeWhitespace() to properly format the output
            return newRoot.NormalizeWhitespace().ToFullString();
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
        public string Documentation { get; set; }
    }

    public enum MemberType
    {
        Method,
        Property,
        Event,
        Indexer
    }
}