using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neemle.OrmLite.Generator;
using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class GeneratorQualityTests
{
    private const string Header = "using System; using Neemle.OrmLite; namespace Fixture;\n";

    private static int counter;

    private static string Entity(string name, string table, string extra = "") =>
        $"[Table(\"{table}\")] public class {name} {{ [Key] public Guid Id {{ get; set; }} {extra} }}\n";

    private static CSharpCompilation Compile(params (string Path, string Text)[] files) =>
        CompileAs("QualityFixture" + Interlocked.Increment(ref counter), files);

    // The schema class is named after the assembly, so two compilations that must give one text share a name.
    private static CSharpCompilation CompileAs(string assembly, params (string Path, string Text)[] files)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(Header + f.Text, path: f.Path));
        return CSharpCompilation.Create(
            assembly, trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static GeneratorDriver Tracking(IIncrementalGenerator generator) =>
        CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()], null, null, GeneratorOptions.Pinned(),
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static IEnumerable<IncrementalStepRunReason> Reasons(GeneratorDriver driver, string step) =>
        driver.GetRunResult().Results[0].TrackedSteps[step].SelectMany(s => s.Outputs).Select(o => o.Reason);

    private static string EntitiesText(params (string Path, string Text)[] files)
    {
        var result = CSharpGeneratorDriver.Create(
                [new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGenerators(CompileAs("QualityOrderFixture", files)).GetRunResult();
        var partitions = result.Results.SelectMany(r => r.GeneratedSources)
            .Where(s => s.HintName.StartsWith("OrmLite.Entities.", StringComparison.Ordinal))
            .OrderBy(s => s.HintName, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(partitions);
        return string.Join("\n", partitions.Select(s => s.SourceText.ToString()));
    }

    [Fact(DisplayName = "ORML-22 an edit in an unrelated file leaves the entity and migration steps unchanged")]
    public void An_unrelated_edit_does_not_regenerate()
    {
        var migration = MigrationClass("First", "0100_first", "Reversible");
        var compilation = Compile(("model.cs", Entity("Doc", "docs") + migration));
        var entities = Tracking(new OrmGenerator()).RunGenerators(compilation);
        var migrations = Tracking(new MigrationGenerator()).RunGenerators(compilation);
        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { }"));

        entities = entities.RunGenerators(edited);
        migrations = migrations.RunGenerators(edited);

        var allowed = new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged };
        Assert.All(Reasons(entities, OrmGenerator.TablesStep), reason => Assert.Contains(reason, allowed));
        var migrationReasons = Reasons(migrations, MigrationGenerator.MigrationsStep);
        Assert.All(migrationReasons, reason => Assert.Contains(reason, allowed));
    }

    [Fact(DisplayName = "ORML-22 an edit of the entity file itself does regenerate (the check above is not empty)")]
    public void An_edit_of_the_entity_file_regenerates()
    {
        var before = Compile(("model.cs", Entity("Doc", "docs")));
        var driver = Tracking(new OrmGenerator()).RunGenerators(before);
        var changed = Header + Entity("Doc", "docs", "public int Rank { get; set; }");
        var after = before.ReplaceSyntaxTree(
            before.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(changed, path: "model.cs"));

        driver = driver.RunGenerators(after);

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(driver, OrmGenerator.TablesStep));
    }

    [Fact(DisplayName = "ORML-22 the generated entity list does not depend on the order of the syntax trees")]
    public void The_entity_order_ignores_the_tree_order()
    {
        var alpha = ("a.cs", Entity("Alpha", "alphas"));
        var beta = ("b.cs", Entity("Beta", "betas"));

        var forward = EntitiesText(alpha, beta);
        var backward = EntitiesText(beta, alpha);

        Assert.Equal(forward, backward);
        Assert.True(forward.IndexOf("AlphaOrm", StringComparison.Ordinal)
            < forward.IndexOf("BetaOrm", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ORML-22 a many-to-many list names its Add and Remove methods with a sensible singular")]
    public void The_singular_of_a_link_name_is_sensible()
    {
        const string members = "[ManyToMany(\"a_addr\", \"a_id\", \"addr_id\")] "
            + "public System.Collections.Generic.List<Addr> Addresses { get; set; } = new(); "
            + "[ManyToMany(\"a_stat\", \"a_id\", \"stat_id\")] "
            + "public System.Collections.Generic.List<Addr> Status { get; set; } = new(); "
            + "[ManyToMany(\"a_tag\", \"a_id\", \"tag_id\")] "
            + "public System.Collections.Generic.List<Addr> Tags { get; set; } = new();";

        var (diagnostics, _, output) = Run(Entity("A", "a", members) + Entity("Addr", "addrs"));

        Assert.Empty(diagnostics);
        var methods = Methods(GeneratedModel.Of(output), "AOrm");
        Assert.Contains("AddAddressAsync", methods);
        Assert.Contains("RemoveAddressAsync", methods);
        Assert.Contains("AddStatusAsync", methods);
        Assert.Contains("AddTagAsync", methods);
        Assert.DoesNotContain("AddAddresseAsync", methods);
        Assert.DoesNotContain("AddStatuAsync", methods);
    }

    [Fact(DisplayName = "ORML-22 a nullable Guid tenant gets ORMLITE007")]
    public void A_nullable_tenant_is_refused()
    {
        var (diagnostics, _, _) = Run(
            "[Table(\"docs\")] [SoftDelete(Tenant = \"Scope\")] public class Doc { [Key] public Guid Id { get; set; } "
            + "public Guid? Scope { get; set; } public DateTime? DeletedAt { get; set; } }\n");

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE007");
        Assert.Contains("nullable", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Scope'", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-22 ORMLITE001 tells what to do instead and no longer points at the generator source")]
    public void The_unmapped_type_message_is_usable()
    {
        var (diagnostics, _, _) = Run(Entity("A", "a", "public System.TimeSpan Span { get; set; }"));

        var message = Assert.Single(diagnostics, d => d.Id == "ORMLITE001").GetMessage();
        Assert.DoesNotContain("OrmGenerator.Classify", message, StringComparison.Ordinal);
        Assert.Contains("[NotAColumn]", message, StringComparison.Ordinal);
        Assert.Contains("Neemle.OrmLite.Jsonb", message, StringComparison.Ordinal);
    }
}
