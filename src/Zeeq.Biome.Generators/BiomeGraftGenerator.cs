#nullable enable

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zeeq.Biome.Generators;

[Generator]
public sealed class BiomeGraftGenerator : IIncrementalGenerator
{
    private const string BiomeGraftAttribute = "Zeeq.Biome.BiomeGraftAttribute";
    private const string GraftAttribute = "Zeeq.Biome.GraftAttribute";
    private const string NoGraftAttribute = "Zeeq.Biome.NoGraftAttribute";

    private static readonly DiagnosticDescriptor DuplicateMethod = new(
        "BIOME001",
        "Biome graft method collision",
        "Blueprint members '{0}' and '{1}' produce the same graft method '{2}' on an overlapping builder",
        "Biome graft",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Rename one graft with [Graft(\"Name\")] so callers can identify the intended blueprint.",
        helpLinkUri: "https://github.com/zeeq-ai/zeeq-biome/issues/20"
    );

    private static readonly DiagnosticDescriptor SameSingularAndPlural = new(
        "BIOME002",
        "Biome graft plural matches singular",
        "Blueprint member '{0}' uses '{1}' for both singular and plural; set [Graft(Plural = \"...\")]",
        "Biome graft",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Choose a distinct plural graft suffix with [Graft(Plural = \"...\")].",
        helpLinkUri: "https://github.com/zeeq-ai/zeeq-biome/issues/20"
    );

    private static readonly DiagnosticDescriptor InvalidCatalog = new(
        "BIOME003",
        "Invalid Biome graft catalog",
        "[BiomeGraft] supports only top-level, non-generic, non-file-local static classes",
        "Biome graft",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Move the catalog to a top-level static class without type parameters or file-local scope.",
        helpLinkUri: "https://github.com/zeeq-ai/zeeq-biome/issues/20"
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var catalogs = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                BiomeGraftAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => CreateCatalogModel(attributeContext)
            )
            .WithTrackingName("BiomeGraftCatalog");

        context.RegisterSourceOutput(
            catalogs,
            static (productionContext, catalog) => EmitCatalog(productionContext, catalog)
        );
    }

    private static CatalogModel CreateCatalogModel(GeneratorAttributeSyntaxContext context)
    {
        var catalog = (INamedTypeSymbol)context.TargetSymbol;
        var isFileLocal =
            context.TargetNode is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.FileKeyword);
        var isSupported =
            catalog.IsStatic
            && catalog.ContainingType is null
            && !catalog.IsGenericType
            && !isFileLocal;

        var members = ImmutableArray.CreateBuilder<BlueprintMemberModel>();
        if (isSupported)
        {
            foreach (var member in catalog.GetMembers())
            {
                if (HasAttribute(member, NoGraftAttribute))
                {
                    continue;
                }

                ITypeSymbol? memberType = member switch
                {
                    IFieldSymbol field
                        when field.IsStatic
                            && field.IsReadOnly
                            && !field.IsConst
                            && !field.IsImplicitlyDeclared => field.Type,
                    IPropertySymbol property
                        when property.GetMethod?.IsStatic == true && property.SetMethod is null =>
                        property.Type,
                    _ => null,
                };

                if (
                    memberType is not INamedTypeSymbol namedType
                    || !TryGetBlueprintKind(
                        namedType,
                        out var kind,
                        out var seedType,
                        out var parentType,
                        out var inputType,
                        out var outputType
                    )
                )
                {
                    continue;
                }

                var (name, plural) = ReadGraftNames(member, member.Name);
                members.Add(
                    new BlueprintMemberModel(
                        member.Name,
                        name,
                        plural ?? Pluralize(name),
                        EscapeIdentifier(member.Name),
                        seedType,
                        parentType,
                        inputType,
                        outputType,
                        kind,
                        GetAccessibility(member.DeclaredAccessibility)
                    )
                );
            }
        }

        var namespaceName = catalog.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : catalog.ContainingNamespace.ToDisplayString();

        return new CatalogModel(
            catalog.Name,
            catalog.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            namespaceName,
            GetAccessibility(catalog.DeclaredAccessibility),
            isSupported,
            context.TargetNode.GetLocation(),
            members.ToImmutable()
        );
    }

    private static void EmitCatalog(SourceProductionContext context, CatalogModel catalog)
    {
        if (!catalog.IsSupported)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidCatalog, catalog.Location));
            return;
        }

        ReportMemberDiagnostics(context, catalog);
        var source = RenderCatalog(catalog);
        var hintName =
            catalog.Namespace.Length == 0
                ? $"{catalog.Name}.Grafts.g.cs"
                : $"{catalog.Namespace.Replace('.', '/')}/{catalog.Name}.Grafts.g.cs";
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static void ReportMemberDiagnostics(
        SourceProductionContext context,
        CatalogModel catalog
    )
    {
        foreach (var member in catalog.Members)
        {
            if (string.Equals(member.Name, member.Plural, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        SameSingularAndPlural,
                        catalog.Location,
                        member.SourceName,
                        member.Name
                    )
                );
            }
        }

        var methods = catalog.Members.SelectMany(member => CreateMethodModels(member)).ToArray();
        for (var i = 0; i < methods.Length; i++)
        {
            for (var j = i + 1; j < methods.Length; j++)
            {
                var left = methods[i];
                var right = methods[j];
                if (
                    left.Name == right.Name
                    && left.Parameters.SequenceEqual(right.Parameters, StringComparer.Ordinal)
                    && ReceiversOverlap(left.Member, right.Member)
                )
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DuplicateMethod,
                            catalog.Location,
                            left.Member.SourceName,
                            right.Member.SourceName,
                            left.Name
                        )
                    );
                }
            }
        }
    }

    private static IEnumerable<MethodModel> CreateMethodModels(BlueprintMemberModel member)
    {
        var customizer = member.InputType ?? member.OutputType;
        yield return new MethodModel(
            $"Add{member.Name}",
            new[] { $"out {member.OutputType}", $"global::System.Action<{customizer}>?" },
            member
        );
        yield return new MethodModel(
            $"Add{member.Plural}",
            new[] { $"out {member.OutputType}[]", "int" },
            member
        );
        yield return new MethodModel(
            $"Add{member.Plural}",
            new[] { $"out {member.OutputType}[]", $"params global::System.Action<{customizer}>[]" },
            member
        );
    }

    private static bool ReceiversOverlap(BlueprintMemberModel left, BlueprintMemberModel right)
    {
        if (left.SeedType != right.SeedType)
        {
            return false;
        }

        if (IsRootKind(left.Kind) || IsRootKind(right.Kind))
        {
            return true;
        }

        return left.ParentType == right.ParentType;
    }

    private static string RenderCatalog(CatalogModel catalog)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using Zeeq.Biome;");
        source.AppendLine();
        if (catalog.Namespace.Length > 0)
        {
            source.Append("namespace ").Append(catalog.Namespace).AppendLine(";");
            source.AppendLine();
        }

        source.AppendLine(
            "/// <summary>Generated Add graft methods for a blueprint catalog.</summary>"
        );
        source
            .Append(catalog.Accessibility)
            .Append(" static class ")
            .Append(catalog.Name)
            .AppendLine("Grafts");
        source.AppendLine("{");

        foreach (var member in catalog.Members)
        {
            RenderMethods(source, catalog, member);
        }

        source.AppendLine("}");
        return source.ToString();
    }

    private static void RenderMethods(
        StringBuilder source,
        CatalogModel catalog,
        BlueprintMemberModel member
    )
    {
        var customizeType = member.InputType ?? member.OutputType;
        var extensionReceiver = IsRootKind(member.Kind)
            ? $"global::Zeeq.Biome.BiomeBuilder<{member.SeedType}, TAnchor>"
            : $"global::Zeeq.Biome.BiomeBuilder<{member.SeedType}, {member.ParentType}[]>";
        var returnType =
            $"global::Zeeq.Biome.BiomeBuilder<{member.SeedType}, {member.OutputType}[]>";
        var rootGeneric = IsRootKind(member.Kind) ? "<TAnchor>" : string.Empty;

        source.AppendLine("    /// <summary>Creates one default blueprint output.</summary>");
        source
            .Append("    ")
            .Append(member.Accessibility)
            .Append(" static ")
            .Append(returnType)
            .Append(' ')
            .Append("Add")
            .Append(member.Name)
            .Append(rootGeneric)
            .Append("(this ")
            .Append(extensionReceiver)
            .Append(" b, out ")
            .Append(member.OutputType)
            .Append(' ')
            .Append(EscapeIdentifier(CamelCase(member.Name)))
            .Append(", global::System.Action<")
            .Append(customizeType)
            .Append(">? customize = null) =>\n")
            .Append("        b.AddOne(")
            .Append(catalog.QualifiedName)
            .Append('.')
            .Append(member.EscapedSourceName)
            .Append(", out ")
            .Append(EscapeIdentifier(CamelCase(member.Name)))
            .AppendLine(", customize);");

        source.AppendLine(
            "    /// <summary>Creates the requested number of blueprint outputs.</summary>"
        );
        source
            .Append("    ")
            .Append(member.Accessibility)
            .Append(" static ")
            .Append(returnType)
            .Append(" Add")
            .Append(member.Plural)
            .Append(rootGeneric)
            .Append("(this ")
            .Append(extensionReceiver)
            .Append(" b, out ")
            .Append(member.OutputType)
            .Append("[] ")
            .Append(EscapeIdentifier(CamelCase(member.Plural)))
            .AppendLine(", int count) =>")
            .Append("        b.Add(")
            .Append(catalog.QualifiedName)
            .Append('.')
            .Append(member.EscapedSourceName)
            .Append(", out ")
            .Append(EscapeIdentifier(CamelCase(member.Plural)))
            .AppendLine(", count);");

        source.AppendLine("    /// <summary>Creates one output per customizer.</summary>");
        source
            .Append("    ")
            .Append(member.Accessibility)
            .Append(" static ")
            .Append(returnType)
            .Append(" Add")
            .Append(member.Plural)
            .Append(rootGeneric)
            .Append("(this ")
            .Append(extensionReceiver)
            .Append(" b, out ")
            .Append(member.OutputType)
            .Append("[] ")
            .Append(EscapeIdentifier(CamelCase(member.Plural)))
            .Append(", params global::System.Action<")
            .Append(customizeType)
            .Append(">[] customize) =>\n")
            .Append("        b.Add(")
            .Append(catalog.QualifiedName)
            .Append('.')
            .Append(member.EscapedSourceName)
            .Append(", out ")
            .Append(EscapeIdentifier(CamelCase(member.Plural)))
            .AppendLine(", customize);")
            .AppendLine();
    }

    private static bool TryGetBlueprintKind(
        INamedTypeSymbol type,
        out BlueprintKind kind,
        out string seedType,
        out string? parentType,
        out string? inputType,
        out string outputType
    )
    {
        var definition = type.OriginalDefinition.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var arguments = type.TypeArguments;
        parentType = null;
        inputType = null;
        switch (definition)
        {
            case "global::Zeeq.Biome.Blueprint<TSeed, T>":
                kind = BlueprintKind.Root;
                seedType = Format(arguments[0]);
                outputType = Format(arguments[1]);
                return true;
            case "global::Zeeq.Biome.InputBlueprint<TSeed, TInput, T>":
                kind = BlueprintKind.RootInput;
                seedType = Format(arguments[0]);
                inputType = Format(arguments[1]);
                outputType = Format(arguments[2]);
                return true;
            case "global::Zeeq.Biome.ChildBlueprint<TSeed, TParent, T>":
                kind = BlueprintKind.Child;
                seedType = Format(arguments[0]);
                parentType = Format(arguments[1]);
                outputType = Format(arguments[2]);
                return true;
            case "global::Zeeq.Biome.ChildInputBlueprint<TSeed, TParent, TInput, T>":
                kind = BlueprintKind.ChildInput;
                seedType = Format(arguments[0]);
                parentType = Format(arguments[1]);
                inputType = Format(arguments[2]);
                outputType = Format(arguments[3]);
                return true;
            default:
                kind = default;
                seedType = string.Empty;
                outputType = string.Empty;
                return false;
        }
    }

    private static string Format(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static bool IsRootKind(BlueprintKind kind) =>
        kind is BlueprintKind.Root or BlueprintKind.RootInput;

    private static bool HasAttribute(ISymbol symbol, string metadataName) =>
        symbol
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

    private static (string Name, string? Plural) ReadGraftNames(ISymbol member, string sourceName)
    {
        var attribute = member
            .GetAttributes()
            .FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == GraftAttribute);
        if (attribute is null)
        {
            return (sourceName, null);
        }

        var name =
            attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;
        var plural =
            attribute
                .NamedArguments.FirstOrDefault(argument => argument.Key == "Plural")
                .Value.Value as string;
        return (string.IsNullOrWhiteSpace(name) ? sourceName : name ?? sourceName, plural);
    }

    private static string Pluralize(string singular)
    {
        if (
            singular.Length >= 2
            && singular.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            && char.IsLetter(singular[singular.Length - 2])
            && !"aeiou".Contains(char.ToLowerInvariant(singular[singular.Length - 2]))
        )
        {
            return singular.Substring(0, singular.Length - 1) + "ies";
        }

        if (
            singular.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("x", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("z", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
        )
        {
            return singular + "es";
        }

        return singular + "s";
    }

    private static string CamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : "@" + identifier;

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };

    private enum BlueprintKind
    {
        Root,
        RootInput,
        Child,
        ChildInput,
    }

    private sealed class BlueprintMemberModel(
        string sourceName,
        string name,
        string plural,
        string escapedSourceName,
        string seedType,
        string? parentType,
        string? inputType,
        string outputType,
        BlueprintKind kind,
        string accessibility
    ) : IEquatable<BlueprintMemberModel>
    {
        public string SourceName { get; } = sourceName;
        public string Name { get; } = name;
        public string Plural { get; } = plural;
        public string EscapedSourceName { get; } = escapedSourceName;
        public string SeedType { get; } = seedType;
        public string? ParentType { get; } = parentType;
        public string? InputType { get; } = inputType;
        public string OutputType { get; } = outputType;
        public BlueprintKind Kind { get; } = kind;
        public string Accessibility { get; } = accessibility;

        public bool Equals(BlueprintMemberModel? other) =>
            other is not null
            && SourceName == other.SourceName
            && Name == other.Name
            && Plural == other.Plural
            && EscapedSourceName == other.EscapedSourceName
            && SeedType == other.SeedType
            && ParentType == other.ParentType
            && InputType == other.InputType
            && OutputType == other.OutputType
            && Kind == other.Kind
            && Accessibility == other.Accessibility;

        public override bool Equals(object? obj) =>
            obj is BlueprintMemberModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SourceName.GetHashCode();
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ Plural.GetHashCode();
                hash = (hash * 397) ^ EscapedSourceName.GetHashCode();
                hash = (hash * 397) ^ SeedType.GetHashCode();
                hash = (hash * 397) ^ (ParentType?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (InputType?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ OutputType.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                return (hash * 397) ^ Accessibility.GetHashCode();
            }
        }
    }

    private sealed class CatalogModel(
        string name,
        string qualifiedName,
        string @namespace,
        string accessibility,
        bool isSupported,
        Location location,
        ImmutableArray<BlueprintMemberModel> members
    ) : IEquatable<CatalogModel>
    {
        public string Name { get; } = name;
        public string QualifiedName { get; } = qualifiedName;
        public string Namespace { get; } = @namespace;
        public string Accessibility { get; } = accessibility;
        public bool IsSupported { get; } = isSupported;
        public Location Location { get; } = location;
        public ImmutableArray<BlueprintMemberModel> Members { get; } = members;

        public bool Equals(CatalogModel? other) =>
            other is not null
            && Name == other.Name
            && QualifiedName == other.QualifiedName
            && Namespace == other.Namespace
            && Accessibility == other.Accessibility
            && IsSupported == other.IsSupported
            && Location.SourceTree?.FilePath == other.Location.SourceTree?.FilePath
            && Location.SourceSpan.Equals(other.Location.SourceSpan)
            && Members.SequenceEqual(other.Members);

        public override bool Equals(object? obj) => obj is CatalogModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name.GetHashCode();
                hash = (hash * 397) ^ QualifiedName.GetHashCode();
                hash = (hash * 397) ^ Namespace.GetHashCode();
                hash = (hash * 397) ^ Accessibility.GetHashCode();
                hash = (hash * 397) ^ IsSupported.GetHashCode();
                hash = (hash * 397) ^ (Location.SourceTree?.FilePath?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Location.SourceSpan.GetHashCode();
                foreach (var member in Members)
                {
                    hash = (hash * 397) ^ member.GetHashCode();
                }
                return hash;
            }
        }
    }

    private sealed class MethodModel(string name, string[] parameters, BlueprintMemberModel member)
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public BlueprintMemberModel Member { get; } = member;
    }
}
