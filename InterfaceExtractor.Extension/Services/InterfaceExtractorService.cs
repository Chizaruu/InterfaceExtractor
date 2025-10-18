using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using InterfaceExtractor.Options;
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
        private readonly ExtractorOptions _options;

        public InterfaceExtractorService(ExtractorOptions options = null)
        {
            _options = options ?? new ExtractorOptions();
        }

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
                    return new List<ExtractedClassInfo>();
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

                    // Extract namespace
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

        public string GenerateInterface(string interfaceName, ExtractedClassInfo classInfo, List<MemberInfo> selectedMembers)
        {
            var sb = new StringBuilder();

            // Add file header if enabled
            if (_options.IncludeFileHeader && !string.IsNullOrWhiteSpace(_options.FileHeaderTemplate))
            {
                var header = _options.FileHeaderTemplate
                    .Replace("{FileName}", $"{interfaceName}.cs")
                    .Replace("{Date}", DateTime.Now.ToString("yyyy-MM-dd"))
                    .Replace("{Time}", DateTime.Now.ToString("HH:mm:ss"))
                    .Replace("\\n", "\n");

                sb.AppendLine(header);
                sb.AppendLine();
            }

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
            sb.AppendLine($"namespace {classInfo.Namespace}{_options.InterfacesNamespaceSuffix}");
            sb.AppendLine("{");

            // Start interface
            sb.AppendLine($"    public interface {interfaceName}");
            sb.AppendLine("    {");

            // Sort and group members if requested
            var membersToGenerate = selectedMembers.ToList();

            if (_options.SortMembers)
            {
                membersToGenerate = membersToGenerate
                    .OrderBy(m => m.Type)
                    .ThenBy(m => m.Name)
                    .ToList();
            }

            if (_options.GroupByMemberType)
            {
                membersToGenerate = membersToGenerate
                    .OrderBy(m => GetMemberTypeOrder(m.Type))
                    .ThenBy(m => m.Name)
                    .ToList();
            }

            // Add members
            for (int i = 0; i < membersToGenerate.Count; i++)
            {
                var member = membersToGenerate[i];

                // Add separator lines between members (except before first)
                if (i > 0)
                {
                    for (int j = 0; j < _options.MemberSeparatorLines; j++)
                    {
                        sb.AppendLine();
                    }
                }

                // Add group comment if grouping is enabled
                if (_options.GroupByMemberType &&
                    (i == 0 || membersToGenerate[i - 1].Type != member.Type))
                {
                    sb.AppendLine($"        // {GetMemberTypeGroupName(member.Type)}");
                }

                // Add XML documentation comment if available
                if (!string.IsNullOrWhiteSpace(member.Documentation))
                {
                    foreach (var line in member.Documentation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
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

            // Extract public methods
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

            // Extract public properties
            var properties = classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                           !p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var property in properties)
            {
                var propType = property.Type.ToString();
                var propName = property.Identifier.Text;
                var documentation = ExtractDocumentation(property);

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
                    accessors.Add("get");
                }

                var accessorList = accessors.Any()
                    ? $" {{ {string.Join("; ", accessors)}; }}"
                    : " { get; }";

                members.Add(new MemberInfo
                {
                    Type = MemberType.Property,
                    Signature = $"{propType} {propName}{accessorList}",
                    Name = propName,
                    ReturnType = propType,
                    Documentation = documentation
                });
            }

            // Extract public events
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

            // Extract public indexers
            var indexers = classDeclaration.Members
                .OfType<IndexerDeclarationSyntax>()
                .Where(i => i.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                           !i.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)));

            foreach (var indexer in indexers)
            {
                var indexerType = indexer.Type.ToString();
                var parameters = indexer.ParameterList.ToString();
                var documentation = ExtractDocumentation(indexer);

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

            // Extract operator overloads (if enabled in options)
            if (_options.IncludeOperatorOverloads)
            {
                var operators = classDeclaration.Members
                    .OfType<OperatorDeclarationSyntax>()
                    .Where(o => o.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

                foreach (var op in operators)
                {
                    var returnType = op.ReturnType.ToString();
                    var operatorToken = op.OperatorToken.Text;
                    var parameters = op.ParameterList.ToString();
                    var documentation = ExtractDocumentation(op);

                    members.Add(new MemberInfo
                    {
                        Type = MemberType.Operator,
                        Signature = $"{returnType} operator {operatorToken}{parameters}",
                        Name = $"operator {operatorToken}",
                        ReturnType = returnType,
                        Documentation = documentation
                    });
                }

                // Extract conversion operators
                var conversions = classDeclaration.Members
                    .OfType<ConversionOperatorDeclarationSyntax>()
                    .Where(c => c.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

                foreach (var conversion in conversions)
                {
                    var conversionType = conversion.Type.ToString();
                    var implicitOrExplicit = conversion.ImplicitOrExplicitKeyword.Text;
                    var parameters = conversion.ParameterList.ToString();
                    var documentation = ExtractDocumentation(conversion);

                    members.Add(new MemberInfo
                    {
                        Type = MemberType.Operator,
                        Signature = $"{implicitOrExplicit} operator {conversionType}{parameters}",
                        Name = $"{implicitOrExplicit} operator {conversionType}",
                        ReturnType = conversionType,
                        Documentation = documentation
                    });
                }
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

        public string AppendInterfaceToClass(string sourceCode, string className, string interfaceName, string interfaceNamespace)
        {
            if (!_options.AutoUpdateClass)
            {
                return sourceCode; // Don't update if disabled
            }

            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
            {
                return sourceCode;
            }

            var classNamespace = classDeclaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            var classNamespaceName = classNamespace?.Name.ToString() ?? "";

            // Determine interface name to use
            string interfaceToAdd;
            bool sameNamespace = classNamespaceName == interfaceNamespace || string.IsNullOrEmpty(interfaceNamespace);

            if (sameNamespace)
            {
                // Same namespace, use simple name
                interfaceToAdd = interfaceName;
            }
            else if (_options.AddUsingDirective)
            {
                // Different namespace but adding using directive, use simple name
                interfaceToAdd = interfaceName;
            }
            else
            {
                // Different namespace and not adding using, use fully qualified name
                interfaceToAdd = $"{interfaceNamespace}.{interfaceName}";
            }

            if (classDeclaration.BaseList != null)
            {
                var existingBases = classDeclaration.BaseList.Types
                    .Select(t => t.ToString())
                    .ToList();

                if (existingBases.Any(b => b.Contains(interfaceName)))
                {
                    return sourceCode;
                }
            }

            ClassDeclarationSyntax newClassDeclaration;

            if (classDeclaration.BaseList == null)
            {
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(interfaceToAdd));

                var baseList = SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType));

                newClassDeclaration = classDeclaration.WithBaseList(baseList);
            }
            else
            {
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(interfaceToAdd));

                var newBaseList = classDeclaration.BaseList.AddTypes(baseType);
                newClassDeclaration = classDeclaration.WithBaseList(newBaseList);
            }

            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            if (_options.AddUsingDirective &&
                !sameNamespace &&
                !string.IsNullOrEmpty(interfaceNamespace))
            {
                var usingDirective = SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(interfaceNamespace));

                var existingUsings = newRoot.Usings
                    .Select(u => u.Name.ToString())
                    .ToList();

                if (!existingUsings.Contains(interfaceNamespace))
                {
                    newRoot = newRoot.AddUsings(usingDirective);
                }
            }

            return newRoot.NormalizeWhitespace().ToFullString();
        }

        private static int GetMemberTypeOrder(MemberType type)
        {
            switch (type)
            {
                case MemberType.Property:
                    return 1;

                case MemberType.Method:
                    return 2;

                case MemberType.Event:
                    return 3;

                case MemberType.Indexer:
                    return 4;

                case MemberType.Operator:
                    return 5;

                default:
                    return 99;
            }
        }

        private static string GetMemberTypeGroupName(MemberType type)
        {
            switch (type)
            {
                case MemberType.Property:
                    return "Properties";

                case MemberType.Method:
                    return "Methods";

                case MemberType.Event:
                    return "Events";

                case MemberType.Indexer:
                    return "Indexers";

                case MemberType.Operator:
                    return "Operators";

                default:
                    return "Members";
            }
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
        Indexer,
        Operator
    }
}