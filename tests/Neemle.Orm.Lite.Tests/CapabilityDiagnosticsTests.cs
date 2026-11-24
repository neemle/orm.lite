using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class CapabilityDiagnosticsTests
{
    private const string Header = "using System; using System.Collections.Generic; using Neemle.Orm.Lite; using Neemle.Orm.Lite.Nodes; namespace Fixture;\n";

    private sealed class ManifestText(string provider, string text) : AdditionalText
    {
        public override string Path { get; } = "/assets/ormlite.capabilities." + provider + ".txt";

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private sealed class Bag(Dictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
        }

        public override AnalyzerConfigOptions GlobalOptions { get; } = new Bag(values);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private static string Manifest(params string[] none)
    {
        var levels = Enum.GetValues<Capability>().Select(c => c + "=" + (none.Contains(c.ToString()) ? "None" : "Native"));
        return string.Join("\n", levels) + "\n";
    }

    private static Options Providers(string list) =>
        new(new Dictionary<string, string> { ["build_property.OrmLiteProviders"] = list, ["build_property.OrmLiteDefaultTenant"] = "ScopeId" });

    private static CSharpCompilation Compile(string source)
    {
        return CSharpCompilation.Create(
            "CapabilityFixture", [CSharpSyntaxTree.ParseText(Header + source)], GeneratorReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<Diagnostic> Generate(string source, string providers, params ManifestText[] manifests)
    {
        var driver = CSharpGeneratorDriver.Create(
            [new OrmGenerator().AsSourceGenerator(), new MigrationGenerator().AsSourceGenerator()],
            manifests, null, Providers(providers));
        return driver.RunGenerators(Compile(source)).GetRunResult().Diagnostics;
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source, string providers, params ManifestText[] manifests)
    {
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CapabilityAnalyzer());
        var withAnalyzers = Compile(source).WithAnalyzers(analyzers, new AnalyzerOptions([.. manifests], Providers(providers)));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private const string LiveEntity =
        "[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] [Index(\"Name\", Unique = true, LiveOnly = true)] public class Doc "
        + "{ [Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; public Jsonb? Extra { get; set; } public DateTime? DeletedAt { get; set; } }\n";

    private static IEnumerable<Diagnostic> Only(ImmutableArray<Diagnostic> all, string id) => all.Where(d => d.Id == id);

    [Fact(DisplayName = "ORMLITE015 names the entity, the LiveOnly index, the capability and the provider when the provider declares PartialIndex as None")]
    public void A_live_only_index_needs_partial_indexes()
    {
        var found = Only(Generate(LiveEntity, "sqlite", new ManifestText("sqlite", Manifest("PartialIndex"))), "ORMLITE015").Single();

        Assert.Equal(DiagnosticSeverity.Error, found.Severity);
        var message = found.GetMessage();
        Assert.Contains("'Doc'", message);
        Assert.Contains("ix_docs_name_live", message);
        Assert.Contains("PartialIndex", message);
        Assert.Contains("'sqlite'", message);
    }

    [Fact(DisplayName = "ORMLITE015 names the [View] entity, the ObjectMarker capability and the provider when the provider declares ObjectMarker as None")]
    public void A_view_needs_object_markers()
    {
        var view = "[View(\"open_work\")] public class OpenWork { public Guid Id { get; set; } }\n";

        var found = Only(Generate(view, "bare", new ManifestText("bare", Manifest("ObjectMarker"))), "ORMLITE015").Single();

        var message = found.GetMessage();
        Assert.Contains("'OpenWork'", message);
        Assert.Contains("ObjectMarker", message);
        Assert.Contains("'bare'", message);
        Assert.Empty(Only(Generate(view, "bare", new ManifestText("bare", Manifest())), "ORMLITE015"));
    }

    [Fact(DisplayName = "ORMLITE015 is silent when every capability the declarations need is Native or Emulated")]
    public void A_capable_provider_is_not_refused()
    {
        var emulated = Manifest().Replace("PartialIndex=Native", "PartialIndex=Emulated");

        Assert.Empty(Only(Generate(LiveEntity, "postgres", new ManifestText("postgres", Manifest())), "ORMLITE015"));
        Assert.Empty(Only(Generate(LiveEntity, "sqlite", new ManifestText("sqlite", emulated)), "ORMLITE015"));
    }

    [Fact(DisplayName = "a Jsonb property needs JsonDocument, [SoftDelete] needs RowHistoryTriggers and ObjectMarker, and declared indexes need CatalogGuard, each reported once per provider")]
    public void Each_declaration_names_its_capability()
    {
        var all = Generate(LiveEntity, "bare", new ManifestText("bare", Manifest("JsonDocument", "RowHistoryTriggers", "ObjectMarker", "CatalogGuard")));

        var messages = Only(all, "ORMLITE015").Select(d => d.GetMessage()).ToList();

        Assert.Equal(4, messages.Count);
        Assert.Contains(messages, m => m.Contains("the Jsonb property 'Extra'", StringComparison.Ordinal) && m.Contains("JsonDocument", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("RowHistoryTriggers", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("ObjectMarker", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("CatalogGuard", StringComparison.Ordinal) && m.Contains("indexes", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "a capability the manifest does not list counts as None, and each listed provider that lacks one is reported by itself")]
    public void A_missing_line_is_none_and_every_provider_is_checked()
    {
        var partial = new ManifestText("a", "AlterConstraint=Native\n");
        var full = new ManifestText("b", Manifest("PartialIndex"));

        var found = Only(Generate(LiveEntity, "a;b", partial, full), "ORMLITE015").Where(d => d.GetMessage().Contains("PartialIndex", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, found.Count);
        Assert.Contains(found, d => d.GetMessage().Contains("'a'", StringComparison.Ordinal));
        Assert.Contains(found, d => d.GetMessage().Contains("'b'", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "without OrmLiteProviders nothing is checked even when manifests are present, and an unmanaged entity needs no index capability")]
    public void No_provider_list_means_no_check()
    {
        var unmanaged = "[Table(\"links\", Managed = false)] [Index(\"Left\")] public class Link { public Guid Left { get; set; } }\n";

        Assert.Empty(Only(Generate(LiveEntity, "", new ManifestText("sqlite", Manifest("PartialIndex"))), "ORMLITE015"));
        Assert.Empty(Only(Generate(unmanaged, "bare", new ManifestText("bare", Manifest("CatalogGuard", "PartialIndex"))), "ORMLITE015"));
    }

    [Fact(DisplayName = "ORMLITE019 warns once for a provider named without a manifest and is silent for one that has it")]
    public void A_provider_without_a_manifest_is_reported()
    {
        var found = Only(Generate(LiveEntity, "postgres;ghost", new ManifestText("postgres", Manifest())), "ORMLITE019").Single();

        Assert.Equal(DiagnosticSeverity.Warning, found.Severity);
        Assert.Contains("'ghost'", found.GetMessage());
    }

    [Fact(DisplayName = "ORMLITE016 names the member, the capability and the provider for a call of a marked member that the provider cannot serve")]
    public async Task A_marked_member_needs_its_capability()
    {
        const string source = @"
[Table(""docs"")] public class Doc { [Key] public Guid Id { get; set; } public Jsonb? Extra { get; set; } }
public static class Use
{
    public static void Locked(OrmContext db) => new Query<Doc>(db, _ => new Doc()).ForUpdate();
    public static void Json(OrmContext db) => new Query<Doc>(db, _ => new Doc()).Where(d => Db.JsonHasKey(d.Extra, ""k""));
    public static void Fine(OrmContext db) => new Query<Doc>(db, _ => new Doc()).Take(1);
}";

        var found = Only(await Analyze(source, "bare", new ManifestText("bare", Manifest("RowLock", "JsonDocument"))), "ORMLITE016").ToList();

        Assert.Equal(2, found.Count);
        Assert.Contains(found, d => d.GetMessage().Contains("Query.ForUpdate", StringComparison.Ordinal) && d.GetMessage().Contains("RowLock", StringComparison.Ordinal));
        Assert.Contains(found, d => d.GetMessage().Contains("Db.JsonHasKey", StringComparison.Ordinal) && d.GetMessage().Contains("JsonDocument", StringComparison.Ordinal));
        Assert.All(found, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.All(found, d => Assert.Contains("'bare'", d.GetMessage()));
    }

    [Fact(DisplayName = "ORMLITE016 is silent for a capable provider, for no provider list and for a call of an unmarked member")]
    public async Task A_capable_or_unlisted_provider_is_not_refused()
    {
        const string source = @"
[Table(""docs"")] public class Doc { [Key] public Guid Id { get; set; } }
public static class Use { public static void Locked(OrmContext db) => new Query<Doc>(db, _ => new Doc()).ForUpdate().Take(1); }";

        Assert.Empty(Only(await Analyze(source, "postgres", new ManifestText("postgres", Manifest())), "ORMLITE016"));
        Assert.Empty(Only(await Analyze(source, "", new ManifestText("bare", Manifest("RowLock"))), "ORMLITE016"));
        Assert.Empty(Only(await Analyze(source, "bare", new ManifestText("bare", Manifest("Upsert"))), "ORMLITE016"));
    }

    [Fact(DisplayName = "every capability a marked member names is one the roster of marked members expects, so a mark cannot name the wrong capability unnoticed")]
    public void The_marks_are_the_expected_ones()
    {
        var expected = new Dictionary<string, Capability>
        {
            ["Db.AnyOf"] = Capability.ArrayParameter,
            ["Db.NullSafeEquals"] = Capability.NullSafeEquality,
            ["Db.JsonKeyEquals"] = Capability.JsonDocument,
            ["Db.JsonHasKey"] = Capability.JsonDocument,
            ["Db.JsonArrayContainsString"] = Capability.JsonDocument,
            ["Db.JsonArrayContainsAny"] = Capability.JsonDocument,
            ["Db.MovedFieldIsNull"] = Capability.JsonDocument,
            ["Db.MovedFieldEquals"] = Capability.JsonDocument,
            ["Db.MovedFieldContains"] = Capability.JsonDocument,
            ["Query`1.ForUpdate"] = Capability.RowLock,
            ["Query`1.ForNoKeyUpdate"] = Capability.KeyPreservingRowLock,
            ["Query`1.UpsertAsync"] = Capability.Upsert,
            ["Query`1.UpdateReturningAsync"] = Capability.Returning,
            ["Query`1.DeleteReturningAsync"] = Capability.Returning,
            ["Query`1.AggregateCountWhere"] = Capability.AggregateFilter,
            ["Grouping`2.CountWhere"] = Capability.AggregateFilter,
            ["Query`1.SetNowPlusMinutes"] = Capability.IntervalArithmetic,
            ["Query`1.OrderByJsonKey"] = Capability.JsonDocument,
            ["Query`1.SetJsonMerge"] = Capability.JsonDocument,
            ["UnitOfWork.LockAsync"] = Capability.NamedLock,
            ["UnitOfWork.LockSharedAsync"] = Capability.NamedLock,
            ["AdvisorySessionLocks.TryLeaseAsync"] = Capability.SessionLock,
        };

        var marked = typeof(OrmContext).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods().Select(m => (Key: t.Name + "." + m.Name, Marks: m.GetCustomAttributes(typeof(RequiresCapabilityAttribute), false).Cast<RequiresCapabilityAttribute>().Select(a => a.Capability).ToList())))
            .Where(p => p.Marks.Count > 0)
            .ToList();

        foreach (var (key, capability) in expected)
            Assert.Contains(capability, marked.Where(p => p.Key == key).SelectMany(p => p.Marks));
        var unexpected = marked.Select(p => p.Key).Except(expected.Keys).Where(k => !k.Contains("ThenBy", StringComparison.Ordinal) && !k.Contains("OrderBy", StringComparison.Ordinal) && !k.Contains("SetJson", StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, string.Join(", ", unexpected));
    }

    private static ImmutableArray<Diagnostic> Audit(string source) => Generate(source + "", "");

    private static string Mig(string name, string reversibility, string body, string members = "") =>
        $"[Migration(\"0001_{name}\", Reversibility.{reversibility})] public sealed class {name} : Migration {{ public override void Up(MigrationBuilder b) {body} {members} }}\n";

    [Fact(DisplayName = "ORMLITE017 warns when a Reversible migration without a Down override holds an operation that has no inverse, and names the operations")]
    public void A_reversible_migration_with_a_lossy_up_is_reported()
    {
        var found = Only(Audit(Mig("Drops", "Reversible", "=> b.CreateTable(\"t\", t => t.Column(\"id\", ColumnType.Uuid)).DropTable(\"u\").DeleteData(\"v\", \"c\", 1);")), "ORMLITE017").Single();

        Assert.Equal(DiagnosticSeverity.Warning, found.Severity);
        Assert.Contains("'Fixture.Drops'", found.GetMessage());
        Assert.Contains("DropTable, DeleteData", found.GetMessage());
        Assert.Contains("0001_Drops", found.GetMessage());
    }

    [Fact(DisplayName = "ORMLITE017 is silent for a Reversible migration that overrides Down, whose Up is invertible, or that cannot be read")]
    public void Agreeing_migrations_are_not_reported()
    {
        var withDown = Mig("A", "Reversible", "=> b.DropTable(\"u\");", "public override void Down(MigrationBuilder b) => b.CreateTable(\"u\", t => { });");
        var invertible = Mig("B", "Reversible", "=> b.CreateIndex(\"ix\", \"t\", \"c\").RenameColumn(\"t\", \"a\", \"b\");");
        var helper = Mig("C", "Reversible", "=> b.Add(SoftDeleteColumnDdl.Up(\"t\", \"v\"));");
        var opaque = Mig("D", "Reversible", "{ Helper(b); } static void Helper(MigrationBuilder b) => b.DropTable(\"x\");");
        var empty = Mig("E", "Reversible", "{ }");

        Assert.Empty(Only(Audit(withDown + invertible + helper + opaque + empty), "ORMLITE017"));
    }

    [Fact(DisplayName = "ORMLITE017 warns when an Irreversible migration holds only operations that have an inverse, and not when it holds one that has none")]
    public void An_irreversible_migration_with_an_invertible_up_is_reported()
    {
        var found = Only(Audit(Mig("Needless", "Irreversible", "=> b.CreateIndex(\"ix\", \"t\", \"c\");") + Mig("Honest", "Irreversible", "=> b.DropColumn(\"t\", \"c\");")), "ORMLITE017").Single();

        Assert.Contains("'Fixture.Needless'", found.GetMessage());
        Assert.Contains("Declare Reversibility.Reversible", found.GetMessage());
    }

    [Fact(DisplayName = "ORMLITE017 treats Raw and InsertData as operations with no inverse")]
    public void Unknown_operations_have_no_inverse()
    {
        var found = Only(Audit(
            Mig("S", "Reversible", "=> b.Raw(\"postgres\", \"x\");") + Mig("I", "Reversible", "=> b.InsertData(\"t\", [\"c\"], [1]);")), "ORMLITE017").ToList();

        Assert.Equal(2, found.Count);
    }

    [Fact(DisplayName = "ORMLITE018 is retired: a provider-scoped Raw statement raises no diagnostic with that id")]
    public void A_provider_scoped_raw_statement_raises_no_retired_diagnostic()
    {
        var found = Only(Audit(Mig("Scoped", "Irreversible", "=> b.Raw(\"postgres\", \"SELECT 1\");")), "ORMLITE018");

        Assert.Empty(found);
    }

    [Fact(DisplayName = "the shipped PostgreSQL capability manifest equals what the provider declares, line for line")]
    public void The_postgres_manifest_is_the_providers_capability_set()
    {
        var path = Path.Combine(RuntimeSources.LibraryRoot(), "src", "Neemle.Orm.Lite.Postgres", "assets", "ormlite.capabilities.postgres.txt");

        Assert.Equal(PostgresProvider.Instance.Capabilities.Manifest(), File.ReadAllLines(path));
    }

    [Fact(DisplayName = "the shipped SQLite capability manifest equals what the provider declares, line for line")]
    public void The_sqlite_manifest_is_the_providers_capability_set()
    {
        var path = Path.Combine(RuntimeSources.LibraryRoot(), "src", "Neemle.Orm.Lite.Sqlite", "assets", "ormlite.capabilities.sqlite.txt");

        Assert.Equal(Sqlite.SqliteProvider.Instance.Capabilities.Manifest(), File.ReadAllLines(path));
    }

    [Fact(DisplayName = "the shipped SQLite manifest makes the generator admit the [SoftDelete] row history, a Jsonb property, a declared index and the object marker for sqlite, and the shipped PostgreSQL one refuses nothing")]
    public void The_shipped_manifests_drive_the_diagnostics()
    {
        var root = RuntimeSources.LibraryRoot();
        var sqliteText = File.ReadAllText(Path.Combine(root, "src", "Neemle.Orm.Lite.Sqlite", "assets", "ormlite.capabilities.sqlite.txt"));
        var postgres = new ManifestText("postgres", File.ReadAllText(Path.Combine(root, "src", "Neemle.Orm.Lite.Postgres", "assets", "ormlite.capabilities.postgres.txt")));

        Assert.Empty(Only(Generate(LiveEntity, "sqlite;postgres", new ManifestText("sqlite", sqliteText), postgres), "ORMLITE015"));
    }

    [Theory(DisplayName = "the shipped SQLite manifest is what the generator reads: declaring one admitted capability None makes it refuse exactly the declaration that needs it")]
    [InlineData("RowHistoryTriggers")]
    [InlineData("JsonDocument")]
    public void The_shipped_sqlite_manifest_is_what_the_generator_reads(string capability)
    {
        var root = RuntimeSources.LibraryRoot();
        var sqliteText = File.ReadAllText(Path.Combine(root, "src", "Neemle.Orm.Lite.Sqlite", "assets", "ormlite.capabilities.sqlite.txt"));
        var line = capability + "=Emulated";
        Assert.Contains(line, sqliteText, StringComparison.Ordinal);

        var refused = Only(Generate(LiveEntity, "sqlite", new ManifestText("sqlite", sqliteText.Replace(line, capability + "=None", StringComparison.Ordinal))), "ORMLITE015")
            .Select(d => d.GetMessage()).ToList();

        var message = Assert.Single(refused);
        Assert.Contains(capability, message, StringComparison.Ordinal);
        Assert.Contains("'sqlite'", message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "ORMLITE015 names the entity, the index, the capability and the provider for a null placement, a collation, a lowered term or a Where predicate the provider declares None")]
    [InlineData("[Index(\"Name desc nulls last\", Name = \"ix_t\")]", "IndexNullsOrdering")]
    [InlineData("[Index(\"Name collate NOCASE\", Name = \"ix_t\")]", "IndexCollation")]
    [InlineData("[Index(\"lower(Name)\", Name = \"ix_t\")]", "IndexExpression")]
    [InlineData("[Index(\"Name\", Where = \"Name is not null\", Name = \"ix_t\")]", "PartialIndex")]
    public void An_index_term_needs_its_capability(string attribute, string capability)
    {
        var source = "[Table(\"docs\")] " + attribute + " public class Doc { [Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; }\n";

        var refused = Only(Generate(source, "sqlite", new ManifestText("sqlite", Manifest(capability))), "ORMLITE015").Select(d => d.GetMessage()).ToList();
        var accepted = Only(Generate(source, "sqlite", new ManifestText("sqlite", Manifest())), "ORMLITE015");

        var message = Assert.Single(refused);
        Assert.Contains("'Doc'", message, StringComparison.Ordinal);
        Assert.Contains("ix_t", message, StringComparison.Ordinal);
        Assert.Contains(capability, message, StringComparison.Ordinal);
        Assert.Contains("'sqlite'", message, StringComparison.Ordinal);
        Assert.Empty(accepted);
    }

    private const string Migrating = @"
public static class Use
{
    public static void Keys(MigrationBuilder b) =>
        b.AddForeignKey(""fk"", ""a"", ""x"", ""b"", ""y"").DropForeignKey(""fk"", ""a"");
    public static void Views(MigrationBuilder b, ViewDefinition v) =>
        b.CreateView(v).ReplaceView(v).DropView(""v"").CreateViews(v).DropViews(v);
    public static void Columns(MigrationBuilder b) =>
        b.AddDecimalColumn(""t"", ""c"", 10, 2).AddJsonbColumn(""t"", ""j"")
            .AddColumn(""t"", ""d"", ColumnType.Decimal).AddColumn(""t"", ""s"", ColumnType.Text);
    public static void Terms(MigrationBuilder b) => b.CreateIndex(""ix"", ""t"",
        IndexTerm.Asc(""a"").NullsFirst(), IndexTerm.Desc(""b"").NullsLast(),
        IndexTerm.Asc(""c"").Collate(""NOCASE""), IndexTerm.LowerOf(""d""));
    public static Node Helper() =>
        LiveOnlyConversionDdl.Up(""t"", new[] { ""a"" }, ""deleted_at"", ""ix_t"", ""ix_t_live"", false, ""0100_x"");
}";

    private static readonly string[] MigrationNeeds =
    [
        "AlterConstraint", "ObjectMarker", "ExactDecimal", "JsonDocument", "IndexNullsOrdering", "IndexCollation",
        "IndexExpression", "PartialIndex",
    ];

    private static async Task<List<string>> MigrationFindings(string provider, string[] none)
    {
        var manifest = new ManifestText(provider, Manifest(none));
        var all = await Analyze(Migrating, provider, manifest);
        return Only(all, "ORMLITE016").Select(d => d.GetMessage()).ToList();
    }

    private static int Naming(List<string> messages, string capability) =>
        messages.Count(m => m.Contains(capability, StringComparison.Ordinal));

    [Fact(DisplayName = "ORMLITE016 names each migration member that needs a capability the provider lacks")]
    public async Task Migration_members_need_their_capabilities()
    {
        var found = await MigrationFindings("bare", MigrationNeeds);

        Assert.Equal(17, found.Count);
        Assert.Equal(3, Naming(found, "AlterConstraint"));
        Assert.Equal(6, Naming(found, "ObjectMarker"));
        Assert.Equal(2, Naming(found, "ExactDecimal"));
        Assert.Equal(1, Naming(found, "JsonDocument"));
        Assert.Equal(2, Naming(found, "IndexNullsOrdering"));
        Assert.Equal(1, Naming(found, "IndexCollation"));
        Assert.Equal(1, Naming(found, "IndexExpression"));
        Assert.Equal(1, Naming(found, "PartialIndex"));
        Assert.Contains(found, m => m.Contains("'MigrationBuilder.AddForeignKey'", StringComparison.Ordinal));
        Assert.Contains(found, m => m.Contains("'IndexTerm.NullsFirst'", StringComparison.Ordinal));
        Assert.Contains(found, m => m.Contains("'LiveOnlyConversionDdl.Up'", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ORMLITE016 with only IndexNullsOrdering as None names just the two nulls terms")]
    public async Task A_provider_without_nulls_ordering_refuses_only_nulls_terms()
    {
        var found = await MigrationFindings("sqlite", ["IndexNullsOrdering"]);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, m => m.Contains("IndexTerm.NullsFirst", StringComparison.Ordinal));
        Assert.Contains(found, m => m.Contains("IndexTerm.NullsLast", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ORMLITE016 is silent for migration members when every capability is supported")]
    public async Task A_capable_provider_is_not_refused_for_migration_members()
    {
        Assert.Empty(await MigrationFindings("postgres", []));
    }

    [Fact(DisplayName = "ORMLITE016 is silent for migration members when no provider list is set")]
    public async Task No_provider_list_means_no_migration_check()
    {
        var manifest = new ManifestText("bare", Manifest(MigrationNeeds));

        Assert.Empty(Only(await Analyze(Migrating, "", manifest), "ORMLITE016"));
    }

    [Fact(DisplayName = "ORMLITE015 for a Jsonb property sits on the entity type instead of having no location")]
    public void A_declaration_diagnostic_has_a_source_location()
    {
        var manifest = new ManifestText("bare", Manifest("JsonDocument"));

        var found = Only(Generate(LiveEntity, "bare", manifest), "ORMLITE015").Single();

        Assert.True(found.Location.IsInSource);
        Assert.Equal("Doc", found.Location.SourceTree!.GetText().ToString(found.Location.SourceSpan));
    }
}
