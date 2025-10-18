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

        public async Task<List<ExtractedClassInfo>> AnalyzeClassesAsync(string filePath, string projectDirectory = null)
        {
            return await Task.Run(() => AnalyzeClasses(filePath, projectDirectory));
        }

        private List<ExtractedClassInfo> AnalyzeClasses(string filePath, string projectDirectory = null)
        {
            try
            {
                var sourceCode = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = tree.GetRoot();

                // Find all public (and optionally internal) classes and records
                var typeDeclarations = new List<TypeDeclarationSyntax>();

                // Add classes
                typeDeclarations.AddRange(root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => IsAccessible(c.Modifiers)));

                // Add records (v1.2.0 feature)
                typeDeclarations.AddRange(root.DescendantNodes()
                    .OfType<RecordDeclarationSyntax>()
                    .Where(r => IsAccessible(r.Modifiers)));

                if (!typeDeclarations.Any())
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

                foreach (var typeDeclaration in typeDeclarations)
                {
                    var typeName = typeDeclaration.Identifier.Text;
                    var isPartial = typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                    var isRecord = typeDeclaration is RecordDeclarationSyntax;

                    // Extract namespace
                    var namespaceDeclaration = typeDeclaration.Ancestors()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .FirstOrDefault();

                    var namespaceName = namespaceDeclaration?.Name.ToString() ?? "DefaultNamespace";

                    // Extract public members from current file
                    var members = ExtractPublicMembers(typeDeclaration);

                    // Handle partial classes (v1.2.0 feature)
                    if (isPartial && _options.AnalyzePartialClasses && !string.IsNullOrEmpty(projectDirectory))
                    {
                        var partialMembers = AnalyzePartialClassFiles(
                            typeName,
                            namespaceName,
                            projectDirectory,
                            filePath);

                        // Merge members, avoiding duplicates
                        var newMembers = partialMembers.Where(pm => !members.Any(m => m.Signature == pm.Signature));
                        members.AddRange(newMembers);
                    }

                    if (members.Any())
                    {
                        results.Add(new ExtractedClassInfo
                        {
                            ClassName = typeName,
                            Namespace = namespaceName,
                            Members = members,
                            Usings = usings,
                            FilePath = filePath,
                            IsPartial = isPartial,
                            IsRecord = isRecord
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

        private bool IsAccessible(SyntaxTokenList modifiers)
        {
            var isPublic = modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            var isInternal = modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword));

            if (isPublic)
                return true;

            // Include internal if option is enabled
            if (_options.IncludeInternalMembers && isInternal)
                return true;

            // If no explicit accessibility, it's internal by default for types
            if (_options.IncludeInternalMembers && !modifiers.Any(m =>
                m.IsKind(SyntaxKind.PublicKeyword) ||
                m.IsKind(SyntaxKind.PrivateKeyword) ||
                m.IsKind(SyntaxKind.ProtectedKeyword) ||
                m.IsKind(SyntaxKind.InternalKeyword)))
            {
                return true;
            }

            return false;
        }

        private List<MemberInfo> AnalyzePartialClassFiles(string className, string namespaceName, string projectDirectory, string currentFile)
        {
            var additionalMembers = new List<MemberInfo>();

            try
            {
                // Search for other .cs files in the project
                var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Equals(currentFile, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in csFiles)
                {
                    try
                    {
                        var sourceCode = File.ReadAllText(file);
                        var tree = CSharpSyntaxTree.ParseText(sourceCode);
                        var root = tree.GetRoot();

                        // Find partial class/record with matching name and namespace
                        var partialTypes = root.DescendantNodes()
                            .OfType<TypeDeclarationSyntax>()
                            .Where(t => t.Identifier.Text == className &&
                                       t.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                       IsAccessible(t.Modifiers))
                            .ToList();

                        foreach (var partialType in partialTypes)
                        {
                            var ns = partialType.Ancestors()
                                .OfType<BaseNamespaceDeclarationSyntax>()
                                .FirstOrDefault()?.Name.ToString() ?? "DefaultNamespace";

                            if (ns == namespaceName)
                            {
                                var members = ExtractPublicMembers(partialType);
                                additionalMembers.AddRange(members);
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be parsed
                    }
                }
            }
            catch
            {
                // If we can't scan for partial files, just continue with what we have
            }

            return additionalMembers;
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

            // Calculate target namespace
            var targetNamespace = $"{classInfo.Namespace}{_options.InterfacesNamespaceSuffix}";

            // Filter out usings that match the target namespace
            var filteredUsings = classInfo.Usings
                .Where(u => !u.Contains($"using {targetNamespace};"))
                .ToList();

            // Add filtered usings
            foreach (var usingDirective in filteredUsings)
            {
                sb.AppendLine(usingDirective);
            }

            if (filteredUsings.Any())
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

                // Add separator lines between members
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

                // Add XML documentation
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
                    sb.AppendLine(needsSemicolon
                        ? $"        {member.Signature};"
                        : $"        {member.Signature}");
                }
            }

            // Close interface and namespace
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateImplementationStub(string interfaceName, string className, ExtractedClassInfo classInfo, List<MemberInfo> selectedMembers)
        {
            var sb = new StringBuilder();

            // Add file header if enabled
            if (_options.IncludeFileHeader && !string.IsNullOrWhiteSpace(_options.FileHeaderTemplate))
            {
                var header = _options.FileHeaderTemplate
                    .Replace("{FileName}", $"{className}.cs")
                    .Replace("{Date}", DateTime.Now.ToString("yyyy-MM-dd"))
                    .Replace("{Time}", DateTime.Now.ToString("HH:mm:ss"))
                    .Replace("\\n", "\n");

                sb.AppendLine(header);
                sb.AppendLine();
            }

            var targetNamespace = $"{classInfo.Namespace}{_options.InterfacesNamespaceSuffix}";

            // Add usings
            foreach (var usingDirective in classInfo.Usings)
            {
                sb.AppendLine(usingDirective);
            }

            // Add using for interface namespace if different
            if (classInfo.Namespace != targetNamespace)
            {
                sb.AppendLine($"using {targetNamespace};");
            }

            if (classInfo.Usings.Any())
            {
                sb.AppendLine();
            }

            // Start namespace
            sb.AppendLine($"namespace {classInfo.Namespace}");
            sb.AppendLine("{");

            // Start class
            sb.AppendLine($"    public class {className} : {interfaceName}");
            sb.AppendLine("    {");

            // Generate stubs for each member
            foreach (var member in selectedMembers)
            {
                sb.AppendLine();

                // Add XML documentation
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

                switch (member.Type)
                {
                    case MemberType.Method:
                        GenerateMethodStub(sb, member);
                        break;

                    case MemberType.Property:
                        GeneratePropertyStub(sb, member);
                        break;

                    case MemberType.Event:
                        GenerateEventStub(sb, member);
                        break;

                    case MemberType.Indexer:
                        GenerateIndexerStub(sb, member);
                        break;
                }
            }

            // Close class and namespace
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateMethodStub(StringBuilder sb, MemberInfo member)
        {
            if (!string.IsNullOrWhiteSpace(member.Constraints))
            {
                sb.AppendLine($"        public {member.Signature}");
                sb.AppendLine($"            {member.Constraints}");
            }
            else
            {
                sb.AppendLine($"        public {member.Signature}");
            }

            sb.AppendLine("        {");

            // Add appropriate return statement or throw NotImplementedException
            if (member.ReturnType == "Task")
            {
                sb.AppendLine($"            return Task.CompletedTask;");
            }
            else if (member.ReturnType != "void")
            {
                sb.AppendLine($"            throw new System.NotImplementedException();");
            }

            sb.AppendLine("        }");
        }

        private static void GeneratePropertyStub(StringBuilder sb, MemberInfo member)
        {
            sb.AppendLine($"        public {member.Signature}");
        }

        private static void GenerateEventStub(StringBuilder sb, MemberInfo member)
        {
            sb.AppendLine($"        public {member.Signature};");
        }

        private static void GenerateIndexerStub(StringBuilder sb, MemberInfo member)
        {
            sb.AppendLine($"        public {member.Signature}");
        }

        private List<MemberInfo> ExtractPublicMembers(TypeDeclarationSyntax typeDeclaration)
        {
            var members = new List<MemberInfo>();

            // Extract primary constructor parameters from records (v1.2.0)
            if (typeDeclaration is RecordDeclarationSyntax recordDeclaration &&
                recordDeclaration.ParameterList != null)
            {
                foreach (var parameter in recordDeclaration.ParameterList.Parameters)
                {
                    var paramType = parameter.Type.ToString();
                    var paramName = parameter.Identifier.Text;

                    // Extract documentation from parameter if available
                    var documentation = ExtractDocumentation(parameter);

                    members.Add(new MemberInfo
                    {
                        Type = MemberType.Property,
                        Signature = $"{paramType} {paramName} {{ get; }}",
                        Name = paramName,
                        ReturnType = paramType,
                        Documentation = documentation
                    });
                }
            }

            // Extract methods
            var methods = typeDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => IsAccessible(m.Modifiers) &&
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

            // Extract properties
            var properties = typeDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => IsAccessible(p.Modifiers) &&
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
                        .Select(accessor =>
                        {
                            var keyword = accessor.Keyword.Text;
                            // Convert 'init' to 'get' for broader compatibility (init accessors in interfaces require implementing types to use init accessors; not supported in all C# versions)
                            return keyword == "init" ? "get" : keyword;
                        })
                        .Distinct() // Remove duplicates if both get and init exist
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

            // Extract events
            var events = typeDeclaration.Members
                .OfType<EventFieldDeclarationSyntax>()
                .Where(e => IsAccessible(e.Modifiers) &&
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

            // Extract indexers
            var indexers = typeDeclaration.Members
                .OfType<IndexerDeclarationSyntax>()
                .Where(i => IsAccessible(i.Modifiers) &&
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

            // Extract operator overloads (if enabled)
            if (_options.IncludeOperatorOverloads)
            {
                var operators = typeDeclaration.Members
                    .OfType<OperatorDeclarationSyntax>()
                    .Where(o => IsAccessible(o.Modifiers));

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
                var conversions = typeDeclaration.Members
                    .OfType<ConversionOperatorDeclarationSyntax>()
                    .Where(c => IsAccessible(c.Modifiers));

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

        private static string ExtractDocumentation(ParameterSyntax parameter)
        {
            var trivia = parameter.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            return trivia != default ? trivia.ToString().Trim() : string.Empty;
        }

        private static string ExtractDocumentation(MemberDeclarationSyntax member)
        {
            var trivia = member.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            return trivia != default ? trivia.ToString().Trim() : string.Empty;
        }

        public string AppendInterfaceToClass(string sourceCode, string className, string interfaceName, string interfaceNamespace)
        {
            if (!_options.AutoUpdateClass)
            {
                return sourceCode;
            }

            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Find class or record
            TypeDeclarationSyntax typeDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (typeDeclaration == null)
            {
                typeDeclaration = root.DescendantNodes()
                    .OfType<RecordDeclarationSyntax>()
                    .FirstOrDefault(r => r.Identifier.Text == className);
            }

            if (typeDeclaration == null)
            {
                return sourceCode;
            }

            var classNamespace = typeDeclaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            var classNamespaceName = classNamespace?.Name.ToString() ?? "";

            // Determine interface name to use
            string interfaceToAdd;
            bool sameNamespace = classNamespaceName == interfaceNamespace || string.IsNullOrEmpty(interfaceNamespace);

            if (sameNamespace)
            {
                interfaceToAdd = interfaceName;
            }
            else if (_options.AddUsingDirective)
            {
                interfaceToAdd = interfaceName;
            }
            else
            {
                interfaceToAdd = $"{interfaceNamespace}.{interfaceName}";
            }

            if (typeDeclaration.BaseList != null)
            {
                var existingBases = typeDeclaration.BaseList.Types
                    .Select(t => t.ToString())
                    .ToList();

                if (existingBases.Any(b => b.Contains(interfaceName)))
                {
                    return sourceCode;
                }
            }

            TypeDeclarationSyntax newTypeDeclaration;

            if (typeDeclaration.BaseList == null)
            {
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(interfaceToAdd));

                var baseList = SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType));

                newTypeDeclaration = typeDeclaration.WithBaseList(baseList);
            }
            else
            {
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(interfaceToAdd));

                var newBaseList = typeDeclaration.BaseList.AddTypes(baseType);
                newTypeDeclaration = typeDeclaration.WithBaseList(newBaseList);
            }

            var newRoot = root.ReplaceNode(typeDeclaration, newTypeDeclaration);

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
                case MemberType.Property: return 1;
                case MemberType.Method: return 2;
                case MemberType.Event: return 3;
                case MemberType.Indexer: return 4;
                case MemberType.Operator: return 5;
                default: return 99;
            }
        }

        private static string GetMemberTypeGroupName(MemberType type)
        {
            switch (type)
            {
                case MemberType.Property: return "Properties";
                case MemberType.Method: return "Methods";
                case MemberType.Event: return "Events";
                case MemberType.Indexer: return "Indexers";
                case MemberType.Operator: return "Operators";
                default: return "Members";
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
        public bool IsPartial { get; set; }
        public bool IsRecord { get; set; }
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