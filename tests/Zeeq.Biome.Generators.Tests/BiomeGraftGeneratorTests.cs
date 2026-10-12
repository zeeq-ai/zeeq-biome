using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyTests;
using VerifyTUnit;
using Zeeq.Biome.Generators;

namespace Zeeq.Biome.Generators.Tests;

public sealed class BiomeGraftGeneratorTests
{
    private const string CoreApi = """
        using System;

        namespace Zeeq.Biome;

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class BiomeGraftAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
        public sealed class GraftAttribute(string? name = null) : Attribute
        {
            public string? Name { get; } = name;
            public string? Plural { get; set; }
        }

        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
        public sealed class NoGraftAttribute : Attribute { }

        public sealed class Blueprint<TSeed, T> where T : class { }
        public sealed class InputBlueprint<TSeed, TInput, T> where TInput : class where T : class { }
        public sealed class ChildBlueprint<TSeed, TParent, T> where TParent : class where T : class { }
        public sealed class ChildInputBlueprint<TSeed, TParent, TInput, T>
            where TParent : class where TInput : class where T : class { }
        public sealed class Sequence<T> { }
        public sealed class BiomeBuilder<TSeed, TAnchor> { }

        public static class RootExtensions
        {
            public static BiomeBuilder<TSeed, T[]> AddOne<TSeed, TAnchor, T>(
                this BiomeBuilder<TSeed, TAnchor> builder, Blueprint<TSeed, T> blueprint,
                out T created, Action<T>? customize = null) where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TAnchor, T>(
                this BiomeBuilder<TSeed, TAnchor> builder, Blueprint<TSeed, T> blueprint,
                out T[] created, int count) where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TAnchor, T>(
                this BiomeBuilder<TSeed, TAnchor> builder, Blueprint<TSeed, T> blueprint,
                out T[] created, params Action<T>[] customize) where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> AddOne<TSeed, TAnchor, TInput, T>(
                this BiomeBuilder<TSeed, TAnchor> builder, InputBlueprint<TSeed, TInput, T> blueprint,
                out T created, Action<TInput>? customize = null) where TInput : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TAnchor, TInput, T>(
                this BiomeBuilder<TSeed, TAnchor> builder, InputBlueprint<TSeed, TInput, T> blueprint,
                out T[] created, int count) where TInput : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TAnchor, TInput, T>(
                this BiomeBuilder<TSeed, TAnchor> builder, InputBlueprint<TSeed, TInput, T> blueprint,
                out T[] created, params Action<TInput>[] customize) where TInput : class where T : class
            { created = null!; return null!; }
        }

        public static class ChildExtensions
        {
            public static BiomeBuilder<TSeed, T[]> AddOne<TSeed, TParent, T>(
                this BiomeBuilder<TSeed, TParent[]> builder, ChildBlueprint<TSeed, TParent, T> blueprint,
                out T created, Action<T>? customize = null) where TParent : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TParent, T>(
                this BiomeBuilder<TSeed, TParent[]> builder, ChildBlueprint<TSeed, TParent, T> blueprint,
                out T[] created, int count) where TParent : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TParent, T>(
                this BiomeBuilder<TSeed, TParent[]> builder, ChildBlueprint<TSeed, TParent, T> blueprint,
                out T[] created, params Action<T>[] customize) where TParent : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> AddOne<TSeed, TParent, TInput, T>(
                this BiomeBuilder<TSeed, TParent[]> builder,
                ChildInputBlueprint<TSeed, TParent, TInput, T> blueprint,
                out T created, Action<TInput>? customize = null)
                where TParent : class where TInput : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TParent, TInput, T>(
                this BiomeBuilder<TSeed, TParent[]> builder,
                ChildInputBlueprint<TSeed, TParent, TInput, T> blueprint, out T[] created, int count)
                where TParent : class where TInput : class where T : class
            { created = null!; return null!; }

            public static BiomeBuilder<TSeed, T[]> Add<TSeed, TParent, TInput, T>(
                this BiomeBuilder<TSeed, TParent[]> builder,
                ChildInputBlueprint<TSeed, TParent, TInput, T> blueprint,
                out T[] created, params Action<TInput>[] customize)
                where TParent : class where TInput : class where T : class
            { created = null!; return null!; }
        }
        """;

    private const string BlueprintForms = """
        using System;
        using Zeeq.Biome;

        namespace GraftFixtures;

        public sealed class Seed { }
        public sealed class District { }
        public sealed class School { }
        public sealed class Campus { }
        public sealed class Building { }
        internal sealed class ImportInput { }
        internal sealed class ImportResult { }
        internal sealed class ClassInput { }
        internal sealed class ClassEntry { }
        internal sealed class LessonInput { }
        internal sealed class Lesson { }
        internal sealed class Category { }
        internal sealed class Bus { }
        internal sealed class Box { }
        internal sealed class Brush { }
        internal sealed class Day { }
        internal sealed class Hidden { }
        internal sealed class GenericBox<T> { }

        [BiomeGraft]
        internal static class Blueprints
        {
            internal static readonly Blueprint<Seed, District> District = null!;
            internal static readonly Blueprint<Seed, District> ArchivedDistrict = District;
            [Graft("Campus", Plural = "Campuses")]
            internal static readonly Blueprint<Seed, Campus> University = null!;
            internal static readonly InputBlueprint<Seed, ImportInput, ImportResult> Import = null!;
            internal static readonly ChildBlueprint<Seed, District, School> School = null!;
            internal static readonly ChildInputBlueprint<Seed, School, LessonInput, Lesson> Lesson = null!;
            internal static readonly Blueprint<Seed, ClassEntry> @class = null!;
            internal static readonly Blueprint<Seed, Category> Category = null!;
            internal static readonly Blueprint<Seed, Bus> Bus = null!;
            internal static readonly Blueprint<Seed, Box> Box = null!;
            internal static readonly Blueprint<Seed, Brush> Brush = null!;
            internal static readonly Blueprint<Seed, Day> Day = null!;
            internal static readonly Blueprint<Seed, GenericBox<string?>> NullableGeneric = null!;

            internal static Blueprint<Seed, Building> Building { get; } = null!;
            #pragma warning disable CS0414
            private static readonly Blueprint<Seed, Hidden> PrivateHidden = null!;
            #pragma warning restore CS0414

            [NoGraft]
            internal static readonly Blueprint<Seed, Hidden> Hidden = null!;

            internal static readonly Action<School> Trait = _ => { };
            internal static readonly Sequence<string> Sequence = null!;
        }

        [BiomeGraft]
        public static class PublicCatalog
        {
            public static readonly Blueprint<Seed, Campus> Campus = null!;
        }
        """;

    private const string InvalidCatalogs = """
        using Zeeq.Biome;

        namespace GraftDiagnostics;

        internal sealed class Seed { }
        internal sealed class School { }
        internal sealed class District { }
        internal sealed class News { }

        [BiomeGraft]
        internal class NotStatic { }

        [BiomeGraft]
        internal static class GenericCatalog<T> { }

        internal static class Outer
        {
            [BiomeGraft]
            internal static class Nested { }
        }

        [BiomeGraft]
        internal static class Diagnostics
        {
            [Graft("School")]
            internal static readonly Blueprint<Seed, School> RootSchool = null!;

            [Graft("School")]
            internal static readonly ChildBlueprint<Seed, District, School> ChildSchool = null!;

            [Graft(Plural = "News")]
            internal static readonly Blueprint<Seed, News> News = null!;
        }
        """;

    [Test]
    public async Task Generates_all_blueprint_forms_and_names()
    {
        var compilation = CreateCompilation(BlueprintForms, treatWarningsAsErrors: true);
        var driver = CreateDriver()
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var diagnostics = updated.GetDiagnostics();
        await Assert
            .That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
        await Assert
            .That(
                diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            )
            .IsEmpty();
        var generated = string.Join(
            "\n",
            driver
                .GetRunResult()
                .Results.Single()
                .GeneratedSources.Select(source => source.SourceText.ToString())
        );
        await Assert.That(generated.Contains("PrivateHidden", StringComparison.Ordinal)).IsFalse();
        await Verify(driver);
    }

    [Test]
    public async Task Reports_graft_diagnostics()
    {
        var driver = CreateDriver().RunGenerators(CreateCompilation(InvalidCatalogs));
        var diagnostics = driver.GetRunResult().Diagnostics;

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "BIOME001")).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "BIOME002")).IsTrue();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "BIOME003")).IsTrue();
        await Assert
            .That(
                diagnostics
                    .Where(diagnostic => diagnostic.Id is "BIOME001" or "BIOME002")
                    .All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            )
            .IsTrue();
        await Assert
            .That(
                diagnostics
                    .Where(diagnostic => diagnostic.Id == "BIOME003")
                    .All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            )
            .IsTrue();
        var collision = diagnostics.Single(diagnostic => diagnostic.Id == "BIOME001");
        await Assert.That(collision.AdditionalLocations.Count).IsEqualTo(1);
        await Assert.That(collision.GetMessage(CultureInfo.InvariantCulture)).Contains("AddSchool");
        await Assert
            .That(collision.GetMessage(CultureInfo.InvariantCulture))
            .Contains("AddSchools");
        await Assert
            .That(diagnostics.Count(diagnostic => diagnostic.Id == "BIOME001"))
            .IsEqualTo(1);
        await Verify(driver);
    }

    [Test]
    public async Task Invalid_graft_names_report_at_members_and_do_not_emit_invalid_code()
    {
        const string invalidNames = """
            using Zeeq.Biome;
            namespace InvalidGraftNames;
            internal sealed class Seed { }
            internal sealed class Entry { }
            [BiomeGraft]
            internal static class Blueprints
            {
                [Graft("Bad Name")]
                internal static readonly Blueprint<Seed, Entry> InvalidSingular = null!;

                [Graft(Plural = "Bad Name")]
                internal static readonly Blueprint<Seed, Entry> InvalidPlural = null!;

                [Graft("123")]
                internal static readonly Blueprint<Seed, Entry> InvalidNumericSingular = null!;

                [Graft(Plural = "456")]
                internal static readonly Blueprint<Seed, Entry> InvalidNumericPlural = null!;

                internal static readonly Blueprint<Seed, Entry> Valid = null!;
            }
            """;

        var compilation = CreateCompilation(invalidNames, treatWarningsAsErrors: true);
        var driver = CreateDriver()
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var diagnostics = driver.GetRunResult().Diagnostics;
        await Assert
            .That(diagnostics.Count(diagnostic => diagnostic.Id == "BIOME004"))
            .IsEqualTo(4);
        await Assert
            .That(
                diagnostics
                    .Where(diagnostic => diagnostic.Id == "BIOME004")
                    .All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            )
            .IsTrue();
        var reportedMemberLines = diagnostics
            .Where(diagnostic => diagnostic.Id == "BIOME004")
            .Select(diagnostic => diagnostic.Location.GetLineSpan().Span.Start.Line)
            .OrderBy(line => line)
            .ToArray();
        await Assert.That(reportedMemberLines.SequenceEqual(new[] { 8, 11, 14, 17 })).IsTrue();
        await Assert
            .That(
                updated
                    .GetDiagnostics()
                    .Where(diagnostic => diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
            )
            .IsEmpty();
        var generated = driver
            .GetRunResult()
            .Results.Single()
            .GeneratedSources.Single()
            .SourceText.ToString();
        await Assert
            .That(generated.Contains("InvalidSingular", StringComparison.Ordinal))
            .IsFalse();
        await Assert.That(generated.Contains("InvalidPlural", StringComparison.Ordinal)).IsFalse();
        await Assert
            .That(generated.Contains("InvalidNumericSingular", StringComparison.Ordinal))
            .IsFalse();
        await Assert
            .That(generated.Contains("InvalidNumericPlural", StringComparison.Ordinal))
            .IsFalse();
        await Assert.That(generated.Contains("AddValid", StringComparison.Ordinal)).IsTrue();
        await Verify(driver);
    }

    [Test]
    public async Task Graft_diagnostics_are_anchored_to_their_source_tree()
    {
        const string source = """
            using Zeeq.Biome;
            namespace GraftPragma;
            internal sealed class Seed { }
            internal sealed class NewsEntry { }
            internal sealed class FishEntry { }
            [BiomeGraft]
            internal static class Blueprints
            {
                #pragma warning disable BIOME002
                [Graft(Plural = "News")]
                internal static readonly Blueprint<Seed, NewsEntry> News = null!;
                #pragma warning restore BIOME002

                [Graft(Plural = "Fish")]
                internal static readonly Blueprint<Seed, FishEntry> Fish = null!;
            }
            """;

        var compilation = CreateCompilation(source, treatWarningsAsErrors: true);
        var sourceTree = compilation.SyntaxTrees.Last();
        var driver = CreateDriver().RunGenerators(compilation);
        var diagnostics = driver
            .GetRunResult()
            .Diagnostics.Where(diagnostic => diagnostic.Id == "BIOME002")
            .ToArray();

        await Assert.That(diagnostics.Length).IsEqualTo(2);
        await Assert
            .That(
                diagnostics.All(diagnostic =>
                    ReferenceEquals(diagnostic.Location.SourceTree, sourceTree)
                )
            )
            .IsTrue();
        var lineNumbers = diagnostics
            .Select(diagnostic => diagnostic.Location.GetLineSpan().Span.Start.Line)
            .OrderBy(line => line)
            .ToArray();
        await Assert.That(lineNumbers[0]).IsEqualTo(10);
        await Assert.That(lineNumbers[1]).IsEqualTo(14);
    }

    [Test]
    public async Task Graft_warnings_remain_visible_for_strict_consumers()
    {
        var compilation = CreateCompilation(InvalidCatalogs, treatWarningsAsErrors: true);
        var driver = CreateDriver().RunGenerators(compilation);
        var strictDiagnostics = driver
            .GetRunResult()
            .Diagnostics.Where(diagnostic => diagnostic.Id is "BIOME001" or "BIOME002")
            .ToArray();

        await Assert.That(strictDiagnostics.Length).IsGreaterThan(0);
        await Assert
            .That(
                strictDiagnostics.All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            )
            .IsTrue();
    }

    [Test]
    public async Task Generated_nullable_code_compiles_and_child_methods_keep_parent_scope()
    {
        const string validUsage = """
            namespace GraftCompile;
            using Zeeq.Biome;
            internal sealed class Seed { }
            internal sealed class District { }
            internal sealed class School { }
            internal sealed class GenericBox<T> { }
            [BiomeGraft]
            internal static class Blueprints
            {
                internal static readonly Blueprint<Seed, District> District = null!;
                internal static readonly ChildBlueprint<Seed, District, School> School = null!;
                internal static readonly Blueprint<Seed, GenericBox<string?>> NullableGeneric = null!;
            }
            internal static class ValidUsage
            {
                internal static void Build(BiomeBuilder<Seed, Seed> builder) =>
                    builder.AddDistrict(out _)
                        .AddSchool(out _)
                        .AddNullableGeneric(out GenericBox<string?> item)
                        .AddNullableGenerics(out GenericBox<string?>[] items, 1);
            }
            """;

        var compilation = CreateCompilation(validUsage, treatWarningsAsErrors: true);
        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var diagnostics = updated.GetDiagnostics();
        await Assert
            .That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
        await Assert
            .That(
                diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            )
            .IsEmpty();

        const string invalidUsage = """
            namespace GraftCompile;
            using Zeeq.Biome;
            internal static class InvalidUsage
            {
                internal static void Build(BiomeBuilder<Seed, Seed> builder) =>
                    builder.AddSchool(out _);
            }
            """;
        var invalidCompilation = compilation.AddSyntaxTrees(Parse(invalidUsage));
        GeneratorDriver invalidDriver = CreateDriver();
        invalidDriver = invalidDriver.RunGeneratorsAndUpdateCompilation(
            invalidCompilation,
            out var invalidUpdated,
            out _
        );
        await Assert
            .That(
                invalidUpdated
                    .GetDiagnostics()
                    .Any(diagnostic =>
                        diagnostic.Id == "CS1929" && diagnostic.Severity == DiagnosticSeverity.Error
                    )
            )
            .IsTrue();
    }

    [Test]
    public async Task Generated_parameter_names_avoid_collisions_with_members_and_parameters()
    {
        const string source = """
            using Zeeq.Biome;
            namespace GraftParameterNames;
            internal sealed class Seed { }
            internal sealed class B { }
            internal sealed class Customize { }
            internal sealed class Amount { }
            [BiomeGraft]
            internal static class Blueprints
            {
                internal static readonly Blueprint<Seed, B> B = null!;
                internal static readonly Blueprint<Seed, Customize> Customize = null!;
                [Graft(Plural = "Count")]
                internal static readonly Blueprint<Seed, Amount> Amount = null!;
            }
            internal static class Usage
            {
                internal static void Build(BiomeBuilder<Seed, Seed> builder)
                {
                    builder.AddB(out var b);
                    builder.AddCustomize(out var customize);
                    builder.AddAmount(out var amount);
                    builder.AddCount(out var count, 1);
                }
            }
            """;

        var compilation = CreateCompilation(source, treatWarningsAsErrors: true);
        var driver = CreateDriver()
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var diagnostics = updated.GetDiagnostics();
        await Assert
            .That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
        await Assert
            .That(
                diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            )
            .IsEmpty();
        await Verify(driver);
    }

    [Test]
    public async Task Unrelated_edit_keeps_graft_transform_cached()
    {
        var unrelatedTree = CSharpSyntaxTree.ParseText(
            "namespace Unrelated; internal sealed class Other { }",
            ParseOptions
        );
        var compilation = CreateCompilation(BlueprintForms).AddSyntaxTrees(unrelatedTree);
        var options = new GeneratorDriverOptions(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new BiomeGraftGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: options
        );
        driver = driver.RunGenerators(compilation);

        var editedCompilation = compilation.ReplaceSyntaxTree(
            unrelatedTree,
            CSharpSyntaxTree.ParseText(
                "namespace Unrelated; internal sealed class Other { /* unrelated edit */ }",
                ParseOptions
            )
        );
        driver = driver.RunGenerators(editedCompilation);
        var runResult = driver.GetRunResult().Results.Single();
        var reasons = runResult
            .TrackedSteps["BiomeGraftSource"]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();

        await Assert.That(reasons.Length > 0).IsTrue();
        await Assert
            .That(reasons.All(reason => reason == IncrementalStepRunReason.Cached))
            .IsTrue();
    }

    private static CSharpGeneratorDriver CreateDriver() =>
        CSharpGeneratorDriver.Create(
            [new BiomeGraftGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions
        );

    private static CSharpCompilation CreateCompilation(
        string source,
        bool treatWarningsAsErrors = false
    ) =>
        CSharpCompilation.Create(
            "BiomeGraftFixture",
            [Parse(CoreApi), Parse(source)],
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                generalDiagnosticOption: treatWarningsAsErrors
                    ? ReportDiagnostic.Error
                    : ReportDiagnostic.Default
            )
        );

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
        {
            if (Path.GetFileName(path) != "Zeeq.Biome.dll")
            {
                yield return MetadataReference.CreateFromFile(path);
            }
        }
    }

    private static SyntaxTree Parse(string source) =>
        CSharpSyntaxTree.ParseText(source, ParseOptions);

    private static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Preview);
}

public static class VerifySetup
{
    [ModuleInitializer]
    public static void Initialize() => VerifySourceGenerators.Initialize();
}
