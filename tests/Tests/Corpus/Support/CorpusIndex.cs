using System.Text;

namespace Neemle.OrmLite.Tests.Corpus;

public static class CorpusIndex
{
    public static IReadOnlyList<string> Unaccounted { get; private set; } = [];

    public static string Build(IReadOnlyList<CorpusEntry> entries)
    {
        var referenced = IlReferences.Scan(typeof(CorpusIndex).Assembly,
            "Neemle.OrmLite.Tests.Corpus.Cases", "Neemle.OrmLite.Tests.Corpus.CorpusRun",
            "Neemle.OrmLite.Tests.Streaming.CiIntegration.StreamCorpusCases");
        var surface = CorpusSurface.Members();
        var exclusions = CorpusSurface.Exclusions;
        var unaccounted = surface.Where(m => !referenced.Contains(m.Key) && !exclusions.ContainsKey(m.Key)).ToList();
        var staleExclusions = exclusions.Keys.Where(k => surface.All(m => m.Key != k) || referenced.Contains(k)).ToList();
        Unaccounted = unaccounted.Select(m => m.Key).Concat(staleExclusions.Select(k => "stale exclusion " + k)).ToList();

        var sb = new StringBuilder();
        sb.Append("# Characterization corpus of the PostgreSQL output\n\n");
        sb.Append("Every file under `Goldens/` is the live output of the library as it was before the redesign, recorded as text. ");
        sb.Append("Nothing here is hand-written: `scripts/CorpusCapture.cs` writes the goldens and this index, and ");
        sb.Append("`CorpusGoldenTests` compares the live output with them on every test run. ");
        sb.Append("The goldens were recorded from `src/` at base commit `0adff7e`, before the redesign; every later slice is proven against them and does not re-record them.\n\n");
        sb.Append("## Use\n\n");
        sb.Append("- Refresh: `dotnet run scripts/CorpusCapture.cs -- capture` (inside the SDK container).\n");
        sb.Append("- Verify without writing: `dotnet run scripts/CorpusCapture.cs -- check`.\n");
        sb.Append("- Capture into another directory, for a determinism comparison: `dotnet run scripts/CorpusCapture.cs -- capture --out <directory>`.\n");
        sb.Append("- A later slice that changes how text is produced keeps these goldens and adapts only the calls in `Cases/`; a golden that must change is a listed, approved deviation.\n\n");

        sb.Append("## Approved deviations\n\n");
        sb.Append("A golden that was changed, removed or added because the library output was wrong, each with its ");
        sb.Append("kind, the old and the new text and the reason. A removed golden has no new text and an added one ");
        sb.Append("has no old text. The list is `CorpusIndex.ApprovedDeviations`; the capture writes it here, so it ");
        sb.Append("cannot drift from the code.\n\n");
        foreach (var deviation in ApprovedDeviations) sb.Append(Row(deviation));
        sb.Append('\n');

        sb.Append("## Entries per group (derived from the capture)\n\n| group | entries |\n| --- | --- |\n");
        foreach (var group in entries.GroupBy(e => e.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
            sb.Append("| `").Append(group.Key).Append("` | ").Append(group.Count()).Append(" |\n");
        sb.Append("| total | ").Append(entries.Count).Append(" |\n\n");

        sb.Append("## What the groups hold\n\n");
        sb.Append("- `query-read`, `query-write`, `query-upsert`, `query-insert-many`, `soft-delete`, `json`, `locks-intervals`: the SQL text and the ordered parameter list that each `Query<T>.Render*` method returns, ");
        sb.Append("for the lambda DSL and the `Db.*` markers, including soft-delete live terms, conflict targets, RETURNING, FILTER, row locks, intervals, array parameters and every JSON clause.\n");
        sb.Append("- `refusals`: the exception type and message of every guard reachable from the render path, the capability refusals of a provider that supports nothing, and the value-type guards.\n");
        sb.Append("- `ddl-builder`, `ddl-helpers`: every `MigrationBuilder` and `TableBuilder` operation, and the DO-block helpers (`ForeignKeyActionDdl`, `LiveOnlyConversionDdl`, `SoftDeleteColumnDdl`) with their refusals.\n");
        sb.Append("- `row-history`, `hash`: the history DDL (preamble, capture function, per-table triggers), the history read, revert and trash statements, and the hash formulas that name migration versions and trigger markers.\n");
        sb.Append("- `executed`: transcripts of the statements that library code builds inside async methods and runs, recorded through a scripted ADO.NET provider (statement text, parameter names and values, connection, transaction brackets). No database is involved.\n");
        sb.Append("- `constants`: every string constant and static read-only string of the surveyed types, private ones included.\n");
        sb.Append("- `generator-*`: for each fixture source set, every generated file, the generator diagnostics, whether the generated code compiles against the runtime, and a dump of the generated schema constants, migration contributions (version names, operations, reversibility) and registry entries read from the compiled generated assembly. ");
        sb.Append("The sets that run the migration runner also record the generated migration runner applied to a scripted database, fresh and partially applied.\n\n");

        sb.Append("## Coverage of the statement-producing surface (derived by `CorpusIndex`)\n\n");
        sb.Append("Method: the surface is every public method (and, for the types marked internal below, every internal method) of the surveyed Runtime types, plus the getters of `Query<T>` and public static getters. ");
        sb.Append("A member counts as referenced when the IL of the code under `Cases/` or the registered ")
            .Append("`Streaming/CiIntegration/StreamCorpusCases` calls it, constructs it or takes its token ")
            .Append("(the `Db.*` markers appear as tokens inside expression trees). ");
        sb.Append("A member that no case references and no exclusion explains makes the capture fail, so the table cannot silently shrink.\n\n");
        sb.Append("| type | members | referenced | excluded |\n| --- | --- | --- | --- |\n");
        foreach (var group in surface.GroupBy(m => m.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var excluded = group.Count(m => exclusions.ContainsKey(m.Key));
            sb.Append("| `").Append(group.Key).Append("` | ").Append(group.Count()).Append(" | ").Append(group.Count(m => referenced.Contains(m.Key))).Append(" | ").Append(excluded).Append(" |\n");
        }
        sb.Append("| total | ").Append(surface.Count).Append(" | ").Append(surface.Count(m => referenced.Contains(m.Key))).Append(" | ").Append(surface.Count(m => exclusions.ContainsKey(m.Key))).Append(" |\n\n");
        sb.Append("Surveyed types that are internal: ").Append(string.Join(", ", CorpusSurface.InternalTypes.Select(t => "`" + CorpusSurface.TypeName(t) + "`"))).Append(".\n\n");

        sb.Append("### Excluded members and why\n\n");
        if (exclusions.Count == 0) sb.Append("None.\n\n");
        else
        {
            sb.Append("| member | reason |\n| --- | --- |\n");
            foreach (var pair in exclusions.OrderBy(p => p.Key, StringComparer.Ordinal))
                sb.Append("| `").Append(pair.Key).Append("` | ").Append(pair.Value).Append(" |\n");
            sb.Append('\n');
        }

        var constants = entries.FirstOrDefault(e => e.Group == "constants" && e.Name == "runtime-string-constants");
        sb.Append("### Constants\n\n- string constants and static read-only strings captured: ").Append(constants is null ? 0 : constants.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length).Append("\n\n");

        sb.Append("### Generator\n\n| set | generated files | diagnostics | executed |\n| --- | --- | --- | --- |\n");
        foreach (var group in entries.Where(e => e.Group.StartsWith("generator-", StringComparison.Ordinal)).GroupBy(e => e.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var generated = group.Count(e => e.Name.EndsWith(".g.cs", StringComparison.Ordinal));
            var diagnostics = group.First(e => e.Name == "diagnostics").Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Count(l => l != "none");
            sb.Append("| `").Append(group.Key).Append("` | ").Append(generated).Append(" | ").Append(diagnostics).Append(" | ").Append(group.Any(e => e.Name == "executed-schema-contributions-and-registry") ? "yes" : "no").Append(" |\n");
        }
        sb.Append('\n');

        sb.Append("## Not covered, by design\n\n");
        foreach (var line in NotCovered) sb.Append("- ").Append(line).Append('\n');
        return sb.ToString();
    }

    internal enum DeviationKind
    {
        Changed,
        Removed,
        Added,
    }

    internal sealed record Deviation(string Case, DeviationKind Kind, string? OldText, string? NewText, string Reason);

    // One list item per deviation, word-wrapped at 120 characters: a deviation holds whole statements and a reason,
    // which a table row cannot wrap.
    internal static string Row(Deviation deviation) =>
        Wrap("- `" + deviation.Case + "`, " + deviation.Kind + ". Old text: " + Cell(deviation.OldText, "(none)")
            + ". New text: " + Cell(deviation.NewText, "(removed)") + ". Reason: " + Escape(deviation.Reason), 120)
        + "\n";

    // Breaks between words only; a continuation line is indented by two spaces, which keeps it inside the list item.
    // A single word longer than the width stays whole.
    internal static string Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' '))
        {
            var joined = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && joined.Length > width)
            {
                lines.Add(current);
                current = "  " + word;
            }
            else
            {
                current = joined;
            }
        }
        lines.Add(current);
        return string.Join('\n', lines);
    }

    internal static string Escape(string text) =>
        text.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Cell(string? text, string absent) => text is null ? absent : "`" + Escape(text) + "`";

    private const string BaselineReason =
        "ORML-54: the generated baseline entry was declared Reversible, so its derived Down dropped every managed "
        + "table and the rows in it, and no down was ever exercised (section 202.7); it is declared Irreversible "
        + "and RevertAsync refuses it by name. The text and the rest of the entry are unchanged.";

    private static Deviation Baseline(string group, string token) =>
        new(
            group + "/executed-schema-contributions-and-registry",
            DeviationKind.Changed,
            "version 0000_baseline_" + token + " reversibility=Reversible down=none",
            "version 0000_baseline_" + token + " reversibility=Irreversible down=none",
            BaselineReason);

    private static readonly Deviation[] BaselineDeviations =
    [
        Baseline("generator-demo", "OrmLiteDemo"),
        Baseline("generator-corpus-entities", "CorpusEntities"),
        Baseline("generator-golden-fixtures", "OrmLiteGoldenFixtures"),
        Baseline("generator-global-namespace", "CorpusGlobalNamespace"),
        Baseline("generator-tenant-option-default", "TenantOptionDefault"),
        Baseline("generator-tenant-option-invalid", "TenantOptionInvalid"),
        Baseline("generator-tenant-option-scope-id", "TenantOptionScope"),
    ];

    // One entry per golden that was changed, removed or added on purpose.
    // Add an entry in the same change that moves the golden.
    private static readonly Deviation[] ApprovedDeviations =
    [
        new(
            "query-read/where-reversed-operands",
            DeviationKind.Changed,
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" < @p0",
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" > @p0",
            "B1 (ORML-2): a comparison with the constant on the left (`3 < n.Rank`) was written with the operator "
            + "not mirrored, so it selected the complement; it now renders `\"rank\" > @p0`. "
            + "The parameter is unchanged."),
        new(
            "query-read/where-constant-left-equals",
            DeviationKind.Added,
            null,
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" = @p0",
            "ORML-51: equality with the constant on the left is symmetric and unchanged; a new case, not a change."),
        new(
            "query-read/where-constant-left-not-equals",
            DeviationKind.Added,
            null,
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" <> @p0",
            "ORML-51: inequality with the constant on the left is symmetric and unchanged; a new case, not a change."),
        new(
            "ddl-builder/insert-data-literal-kinds",
            DeviationKind.Changed,
            "INSERT INTO \"samples\" (\"s\", \"n\", \"d\", \"b\", \"z\", \"g\", \"l\") VALUES "
            + "('it''s', 5, 1.5, true, NULL, a0000000-0000-0000-0000-00000000000a, 9), "
            + "('', -1, 0.25, false, NULL, b0000000-0000-0000-0000-00000000000b, 0)",
            "INSERT INTO \"samples\" (\"s\", \"n\", \"d\", \"b\", \"z\", \"g\", \"l\") VALUES "
            + "('it''s', 5, 1.5, true, NULL, 'a0000000-0000-0000-0000-00000000000a', 9), "
            + "('', -1, 0.25, false, NULL, 'b0000000-0000-0000-0000-00000000000b', 0)",
            "B7 (ORML-8): InsertData wrote a Guid as bare text, which is a syntax error; it is now a "
            + "single-quoted lowercase D-format string. Nothing else on the line moves."),
        new(
            "dialect/ddl-dialect",
            DeviationKind.Changed,
            "[10] Literal(guid) = a0000000-0000-0000-0000-00000000000a",
            "[10] Literal(guid) = 'a0000000-0000-0000-0000-00000000000a'",
            "B7 (ORML-8): PgDdlDialect.Literal reaches the same literal function as InsertData, so a Guid is "
            + "quoted there too. Lines [0]-[9] are unchanged."),
        new(
            "ddl-builder/insert-data-no-rows",
            DeviationKind.Removed,
            "INSERT INTO \"samples\" (\"id\") VALUES ",
            null,
            "B46 (ORML-8): an InsertData without rows rendered a trailing VALUES, which is not valid SQL; the "
            + "builder now refuses it before recording a node, see "
            + "refusals/migration-builder-insert-data-without-rows."),
        new(
            "ddl-builder/delete-data-null-and-quote-values",
            DeviationKind.Removed,
            "DELETE FROM \"samples\" WHERE \"name\" IN ('it''s', NULL)",
            null,
            "B46 (ORML-8): a DeleteData with a null value rendered a list that never matches NULL, so the delete "
            + "silently did less than written; the builder now refuses it, see "
            + "refusals/migration-builder-delete-data-with-a-null-value."),
        new(
            "refusals/migration-builder-insert-data-without-rows",
            DeviationKind.Added,
            null,
            "System.ArgumentException: InsertData needs at least one row. (Parameter 'rows')",
            "B46 (ORML-8): an InsertData without rows is refused before a node is recorded."),
        new(
            "refusals/migration-builder-insert-data-without-columns",
            DeviationKind.Added,
            null,
            "System.ArgumentException: InsertData needs at least one column. (Parameter 'columns')",
            "B46 (ORML-8): an InsertData without columns is refused before a node is recorded."),
        new(
            "refusals/migration-builder-delete-data-with-a-null-value",
            DeviationKind.Added,
            null,
            "System.ArgumentException: DeleteData cannot match a null value: an IN list never matches NULL, "
            + "so the delete would silently skip it. (Parameter 'values')",
            "B46 (ORML-8): a DeleteData with a null value is refused before a node is recorded."),
        new(
            "ddl-builder/insert-data-temporal-enum-and-char-kinds",
            DeviationKind.Added,
            null,
            "INSERT INTO \"samples\" (\"at\", \"shifted\", \"day\", \"status\", \"c\", \"q\", \"doc\") VALUES "
            + "('2024-01-02T10:00:00.0000000Z', '2024-01-02T04:30:00.0000000Z', '2024-02-29', 'Active', 'x', '''', "
            + "'{\"k\":\"it''s\"}'), ('2024-01-02T10:00:00.1234567Z', '2024-01-02T18:00:00.0000000Z', "
            + "'2024-12-31', 'Archived', 'y', 'z', '{}')",
            "B7 (ORML-8): a moment, an offset moment, a day, an enum member, a char and a document are written "
            + "as quoted text."),
        new(
            "ddl-builder/delete-data-guid-enum-and-date-values",
            DeviationKind.Added,
            null,
            "DELETE FROM \"samples\" WHERE \"k\" IN "
            + "('a0000000-0000-0000-0000-00000000000a', 'Active', '2024-02-29')",
            "B7 (ORML-8): a DeleteData matches a Guid, an enum member and a day by their quoted text."),
        new(
            "refusals/ddl-literal-non-finite-number",
            DeviationKind.Added,
            null,
            "System.ArgumentException: NaN has no portable literal; write a finite number. (Parameter 'value')",
            "B7 (ORML-8): NaN and the infinities have no portable literal and are refused, naming the value."),
        new(
            "refusals/ddl-literal-unsupported-type",
            DeviationKind.Added,
            null,
            "System.NotSupportedException: System.TimeSpan has no literal form; write a string, a number, a flag, "
            + "a character, a Guid, a date or time, a Jsonb or an enum member.",
            "B7 (ORML-8): a value of a type with no literal form is refused, naming the type; there is no "
            + "bare fallback."),
        new(
            "generator-diagnostics/diagnostics",
            DeviationKind.Changed,
            "28 diagnostic lines; no ORMLITE022 line",
            "29 diagnostic lines; one added after the ORMLITE018 line: "
            + "ORMLITE022 Error Declarations.cs:103: 'SoftWithTwoKeys' declares 2 key properties (Id, OtherId). "
            + "Orm.Lite supports one key per entity: a get, update or delete by key on several keys would act on "
            + "every row that shares the first, so none is generated for 'SoftWithTwoKeys'. Keep one [Key] "
            + "(a property named Id is a key even without [Key]: remove the other [Key], or rename the Id property "
            + "and keep its column with [Column]), declare no key to insert and query only, or address rows by "
            + "their columns through Query().Where(...).",
            "B4 (ORML-5): an entity with more than one key property is refused with ORMLITE022; the ORMLITE008 line "
            + "for SoftWithTwoKeys is unchanged and both diagnostics now appear for it."),
        new(
            "generator-diagnostics/ormlite.entities.g.cs",
            DeviationKind.Changed,
            "SoftWithTwoKeysOrm declared DeleteAsync(db, Guid key), GetAsync(db, Guid key), "
            + "GetAsync(db, conn, Guid key) and DeleteAsync(db, conn, Guid key), each built on the first key Id alone",
            "SoftWithTwoKeysOrm declares none of the four; its Descriptor, Map, Values, Column, Query and both "
            + "InsertAsync are unchanged",
            "B4 (ORML-5): a get or delete by key on several keys acted on every row sharing the first key, so the "
            + "generator withholds the keyed members of a refused entity."),
        new(
            "executed/constraint-columns-* (3 goldens: from-a-constraint, falls-back-to-an-index, unknown-name; the "
            + "constraint statement)",
            DeviationKind.Changed,
            "WHERE k.conname = @n AND pg_table_is_visible(k.conrelid) ORDER BY c.ord",
            "WHERE k.conname = @n AND k.contype IN ('p', 'u', 'x') AND pg_table_is_visible(k.conrelid) ORDER BY c.ord",
            "B45 (ORML-31): the constraint was found by its name and visibility alone, so a check or foreign key "
            + "constraint of that name answered with the columns it constrains; only a primary key, unique or "
            + "exclusion constraint is read now. Parameters and the outcome of these cases are unchanged."),
        new(
            "executed/constraint-columns-* (2 goldens: falls-back-to-an-index, unknown-name; the index statement)",
            DeviationKind.Changed,
            "AND pg_table_is_visible(r.oid) AND c.attnum > 0 ORDER BY c.ord",
            "AND pg_table_is_visible(r.oid) AND c.attnum > 0 AND c.ord <= i.indnkeyatts ORDER BY c.ord",
            "B45 (ORML-31): indkey lists the key columns and then the INCLUDE columns, so a unique index with an "
            + "INCLUDE column reported its included column as a colliding one; only the first indnkeyatts columns "
            + "are read now. Parameters and the outcome of these cases are unchanged."),
        new(
            "refusals/predicate-range-over-an-enum-column",
            DeviationKind.Added,
            null,
            "System.NotSupportedException: " + EnumRange("Status (CorpusStatus)", ">="),
            EnumReason),
        new(
            "refusals/predicate-range-over-an-enum-column-as-integers",
            DeviationKind.Added,
            null,
            "System.NotSupportedException: " + EnumRange("Status (CorpusStatus)", ">"),
            EnumReason),
        new(
            "refusals/predicate-range-over-a-nullable-enum-column-constant-on-the-left",
            DeviationKind.Added,
            null,
            "System.NotSupportedException: " + EnumRange("Review (CorpusStatus?)", "<"),
            EnumReason),
        new(
            "refusals/order-by-an-enum-column",
            DeviationKind.Added,
            null,
            "System.NotSupportedException: " + EnumOrder("Status (CorpusStatus)"),
            EnumReason),
        new(
            "refusals/order-by-descending-a-nullable-enum-column",
            DeviationKind.Added,
            null,
            "System.NotSupportedException: " + EnumOrder("Review (CorpusStatus?)"),
            EnumReason),
        new(
            "ddl-builder/create-table-all-column-shapes",
            DeviationKind.Changed,
            "CREATE TABLE IF NOT EXISTS \"samples\" (\"id\" uuid NOT NULL PRIMARY KEY, "
            + "\"name\" text NOT NULL DEFAULT ''::text, \"rank\" integer DEFAULT 0, \"note\" text, "
            + "\"created_at\" timestamp with time zone NOT NULL DEFAULT now(), \"odd\"\"name\" text)",
            "CREATE TABLE IF NOT EXISTS \"samples\" (\"id\" uuid NOT NULL PRIMARY KEY, "
            + "\"name\" text NOT NULL DEFAULT '', \"rank\" integer DEFAULT 0, \"note\" text, "
            + "\"created_at\" timestamp with time zone NOT NULL DEFAULT now(), \"odd\"\"name\" text)",
            "B62 (ORML-44): the column type and default spelled as text are gone from the builders; the case now "
            + "builds the same table with ColumnType and Defaults, and a typed text default is the plain literal "
            + "'' where the spelled one carried a ::text cast. Nothing else on the line moves."),
        new(
            "generator-corpus-entities/diagnostics",
            DeviationKind.Changed,
            "4 diagnostic lines: one ORMLITE017 and three ORMLITE018 (Migrations.cs:34, :8 and :9)",
            "1 diagnostic line: the ORMLITE017 line; the three ORMLITE018 lines are gone",
            "B62 (ORML-44): ORMLITE018 is retired with the members it reported, so the fixture's Sql, text AddColumn "
            + "and text Column calls (now Raw with a provider and the typed overloads) raise no warning."),
        new(
            "generator-diagnostics/diagnostics",
            DeviationKind.Changed,
            "29 diagnostic lines, among them 'ORMLITE018 Warning Declarations.cs:155: ...calls Sql(text)...'",
            "28 diagnostic lines: the ORMLITE018 line is gone and every other line is unchanged",
            "B62 (ORML-44): ORMLITE018 is retired with Sql(text); the fixture migration now calls Raw with a provider "
            + "and no line of the fixture moved."),
        new(
            "generator-corpus-entities/migration-runner-fresh-database",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-corpus-entities/migration-runner-partially-applied",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-demo/migration-runner-fresh-database",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-demo/migration-runner-partially-applied",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-global-namespace/migration-runner-fresh-database",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-global-namespace/migration-runner-partially-applied",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-golden-fixtures/migration-runner-fresh-database",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        new(
            "generator-golden-fixtures/migration-runner-partially-applied",
            DeviationKind.Changed,
            HistoryBeforeLock,
            HistoryAfterLock,
            HistoryReason),
        OrderNulls(
            "query-read/order-by-ascending",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC NULLS LAST"),
        OrderNulls(
            "query-read/order-by-descending",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"rank\" DESC",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"rank\" DESC NULLS LAST"),
        OrderNulls(
            "query-read/order-then-by-chain",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC, \"rank\" DESC, \"id\" ASC",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC NULLS LAST, \"rank\" DESC NULLS LAST, \"id\" A"
            + "SC NULLS LAST"),
        OrderNulls(
            "query-read/order-by-replaces-earlier-order",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"views\" DESC",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"views\" DESC NULLS LAST"),
        OrderNulls(
            "query-read/order-by-predicate",
            "SELECT * FROM \"corpus_notes\" ORDER BY (\"rank\" > @p0) ASC",
            "SELECT * FROM \"corpus_notes\" ORDER BY (\"rank\" > @p0) ASC NULLS LAST"),
        OrderNulls(
            "query-read/order-then-by-predicate",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC, (\"active\" = true) ASC",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC NULLS LAST, (\"active\" = true) ASC NULLS LAST"),
        OrderNulls(
            "query-read/order-by-predicate-with-parameter-before-where-parameter",
            "SELECT * FROM \"corpus_notes\" WHERE \"title\" = @p1 ORDER BY (\"title\" = @p0) ASC",
            "SELECT * FROM \"corpus_notes\" WHERE \"title\" = @p1 ORDER BY (\"title\" = @p0) ASC NULLS LAST"),
        OrderNulls(
            "query-read/limit-full-statement-soft",
            "SELECT * FROM \"corpus_tasks\" WHERE \"deleted_at\" IS NULL AND \"rank\" > @p0 ORDER BY \"rank\" DES"
            + "C, \"name\" ASC LIMIT 5 OFFSET 10",
            "SELECT * FROM \"corpus_tasks\" WHERE \"deleted_at\" IS NULL AND \"rank\" > @p0 ORDER BY \"rank\" DES"
            + "C NULLS LAST, \"name\" ASC NULLS LAST LIMIT 5 OFFSET 10"),
        OrderNulls(
            "query-read/project-columns-soft-where-order",
            "SELECT \"id\", \"rank\" FROM \"corpus_tasks\" WHERE \"deleted_at\" IS NULL AND \"rank\" > @p0 ORDER "
            + "BY \"name\" ASC LIMIT 3",
            "SELECT \"id\", \"rank\" FROM \"corpus_tasks\" WHERE \"deleted_at\" IS NULL AND \"rank\" > @p0 ORDER "
            + "BY \"name\" ASC NULLS LAST LIMIT 3"),
        OrderNulls(
            "locks-intervals/lock-for-update-with-order-limit-offset",
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" > @p0 ORDER BY \"title\" ASC LIMIT 2 OFFSET 1 FOR UPDA"
            + "TE",
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" > @p0 ORDER BY \"title\" ASC NULLS LAST LIMIT 2 OFFSET"
            + " 1 FOR UPDATE"),
        OrderNulls(
            "locks-intervals/lock-for-no-key-update-with-order-limit",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"rank\" DESC LIMIT 1 FOR NO KEY UPDATE",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"rank\" DESC NULLS LAST LIMIT 1 FOR NO KEY UPDATE"),
        OrderNulls(
            "json/order-json-key-then-by",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC, (\"payload\" ->> @p0) DESC NULLS LAST",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC NULLS LAST, (\"payload\" ->> @p0) DESC NULLS L"
            + "AST"),
        OrderNulls(
            "json/order-json-key-of-then-by",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC, (SELECT (CASE WHEN pg_input_is_valid((s0.\"do"
            + "c\" ->> @p0), 'numeric') THEN (s0.\"doc\" ->> @p0) END)::numeric FROM \"corpus_tags\" s0 WHERE s0.\""
            + "note_id\" = \"corpus_notes\".\"id\") DESC NULLS LAST",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC NULLS LAST, (SELECT (CASE WHEN pg_input_is_val"
            + "id((s0.\"doc\" ->> @p0), 'numeric') THEN (s0.\"doc\" ->> @p0) END)::numeric FROM \"corpus_tags\" s0 "
            + "WHERE s0.\"note_id\" = \"corpus_notes\".\"id\") DESC NULLS LAST"),
        OrderNulls(
            "executed/query-constructor-maps-the-rows-it-reads",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC",
            "SELECT * FROM \"corpus_notes\" ORDER BY \"title\" ASC NULLS LAST"),
        OrderNulls(
            "executed/query-to-list-empty",
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" > @p0 ORDER BY \"title\" ASC LIMIT 5",
            "SELECT * FROM \"corpus_notes\" WHERE \"rank\" > @p0 ORDER BY \"title\" ASC NULLS LAST LIMIT 5"),
        new(
            "query-upsert/do-update-increment",
            DeviationKind.Changed,
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\", \"rank\") VALUES (@p0, @p1, @p2) ON CONFLICT (\"id"
            + "\") DO UPDATE SET \"views\" = \"corpus_notes\".\"views\" + 3",
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\", \"rank\") VALUES (@p0, @p1, @p2) ON CONFLICT (\"id"
            + "\") DO UPDATE SET \"views\" = COALESCE(\"corpus_notes\".\"views\", 0) + 3",
            IncrementReason),
        new(
            "query-upsert/do-update-mixed-sets",
            DeviationKind.Changed,
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\", \"rank\") VALUES (@p0, @p1, @p2) ON CONFLICT (\"id"
            + "\") DO UPDATE SET \"title\" = @p3, \"views\" = \"corpus_notes\".\"views\" + 1, \"created_at\" = now("
            + "), \"rank\" = @p4",
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\", \"rank\") VALUES (@p0, @p1, @p2) ON CONFLICT (\"id"
            + "\") DO UPDATE SET \"title\" = @p3, \"views\" = COALESCE(\"corpus_notes\".\"views\", 0) + 1, \"create"
            + "d_at\" = now(), \"rank\" = @p4",
            IncrementReason),
        new(
            "executed/query-upsert-do-update-returning",
            DeviationKind.Changed,
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\") VALUES (@p0, @p1) ON CONFLICT (\"id\") DO UPDATE SE"
            + "T \"title\" = @p2, \"views\" = \"corpus_notes\".\"views\" + 1 RETURNING \"id\", \"title\"",
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\") VALUES (@p0, @p1) ON CONFLICT (\"id\") DO UPDATE SE"
            + "T \"title\" = @p2, \"views\" = COALESCE(\"corpus_notes\".\"views\", 0) + 1 RETURNING \"id\", \"title"
            + "\"",
            IncrementReason),
        new(
            "query-upsert/do-update-without-sets-becomes-do-nothing",
            DeviationKind.Removed,
            "INSERT INTO \"corpus_notes\" (\"id\", \"title\", \"rank\") VALUES (@p0, @p1, @p2) ON CONFLICT (\"id"
            + "\") DO NOTHING",
            null,
            NoSetsReason),
        new(
            "query-upsert/soft-do-update-without-sets-has-no-guard",
            DeviationKind.Removed,
            "INSERT INTO \"corpus_tasks\" (\"id\", \"tenant_id\", \"name\", \"rank\") VALUES (@p0, @p1, @p2, @p3)"
            + " ON CONFLICT (\"id\") DO NOTHING",
            null,
            NoSetsReason),
        new(
            "refusals/upsert-update-without-sets",
            DeviationKind.Added,
            null,
            "System.InvalidOperationException: " + NoSetsMessage,
            NoSetsReason),
        new(
            "refusals/soft-upsert-update-without-sets",
            DeviationKind.Added,
            null,
            "System.InvalidOperationException: " + NoSetsMessage,
            NoSetsReason),
        new(
            "generator-diagnostics/diagnostics",
            DeviationKind.Changed,
            "the two ORMLITE001 lines (UnmappedType.Bag, UnmappedType.Span) say: 'Add a mapping to "
            + "OrmGenerator.Classify, or type the property as Neemle.OrmLite.Jsonb to store it as a jsonb column.'",
            "the two ORMLITE001 lines say: 'Use a supported type (string, int, long, bool, double, decimal, Guid, "
            + "DateTime, DateOnly or an enum, each also nullable), type the property as Neemle.OrmLite.Jsonb to "
            + "store it as a jsonb column, or mark it [NotAColumn] to leave it out.'; every other line is unchanged",
            "B29 (ORML-22): the old text pointed at a method of the generator source, which a user of the package "
            + "cannot change; the message now names what the user can do. The property, location and the rest of "
            + "the text are unchanged."),
        new(
            "ddl-builder/insert-data-backslash-and-quote-text",
            DeviationKind.Added,
            null,
            "INSERT INTO \"samples\" (\"a\", \"b\", \"c\") VALUES "
            + "(E'a\\\\b', E'x\\\\''); DROP TABLE t; --', E'\\\\'), "
            + "('plain', E'\\\\', E'{\"k\":\"a\\\\\"b\"}')",
            BackslashReason),
        new(
            "ddl-builder/delete-data-backslash-text",
            DeviationKind.Added,
            null,
            "DELETE FROM \"samples\" WHERE \"k\" IN (E'a\\\\b', 'plain', E'end\\\\')",
            BackslashReason),
        new(
            "refusals/ddl-literal-string-with-nul",
            DeviationKind.Added,
            null,
            "System.ArgumentException: a string or character value holds a NUL character (U+0000), which no SQL "
            + "text literal can carry; remove it from the value. (Parameter 'value')",
            "B7 follow-up (ORML-47): a string, a char or a document holding U+0000 has no SQL text literal "
            + "(PostgreSQL rejects it, SQLite cuts the statement at it); it is refused by name before a node is "
            + "recorded. The message never carries the value."),
        new(
            "refusals/ddl-literal-string-with-a-lone-surrogate",
            DeviationKind.Added,
            null,
            "System.ArgumentException: a string or character value holds half of a surrogate pair (a lone "
            + "surrogate), which is not valid text and has no SQL text literal; remove it from the value. "
            + "(Parameter 'value')",
            "B7 follow-up (ORML-47): a string or char holding half of a surrogate pair is not valid text and "
            + "no driver writes it faithfully; it is refused by name before a node is recorded. A well-formed "
            + "pair is unchanged."),
        .. CorpusLoweringDeviations.All,
        .. CorpusLegacyDialectDeviations.All,
        .. CorpusTenantEnumDeviations.All,
        .. CorpusIdentityDeviations.All,
        .. CorpusNeutralMessageDeviations.All,
        .. BaselineDeviations,
    ];

    private const string EnsureHistory =
        "CREATE TABLE IF NOT EXISTS \"schema_migrations\" (\"version\" text NOT NULL PRIMARY KEY, "
        + "\"applied_at\" timestamp with time zone NOT NULL DEFAULT now())";

    private const string TakeLock = "SELECT pg_advisory_lock(727274)";

    private const string HistoryBeforeLock = "statement #2 is " + EnsureHistory + " and statement #3 is " + TakeLock;

    private const string HistoryAfterLock =
        "statement #2 is " + TakeLock + " and statement #3 is " + EnsureHistory
        + "; no other statement, parameter or transaction marker moves";

    private const string HistoryReason =
        "B31 (ORML-23): the PostgreSQL runner created the history table before it took the advisory lock, so "
        + "replicas migrating an empty database could collide on the catalog; the lock now comes first, and the "
        + "two statements trade places.";

    private const string IncrementReason =
        "B15 (ORML-14): an upsert Increment added to the stored column as it stood, so a counter that was NULL "
        + "stayed NULL however often the conflict hit, unlike SetIncrement; it now counts from zero, "
        + "COALESCE(column, 0) + n. Nothing else on the line moves.";

    private const string NoSetsReason =
        "B16 (ORML-14): an OnConflict.DoUpdate that names no UpsertSet was written as DO NOTHING without a word "
        + "(on a soft-delete entity also without the live-row guard); it is now refused by name, so the golden "
        + "that pinned the old text is removed and the refusal is added.";

    private const string NoSetsMessage =
        "OnConflict.DoUpdate names no UpsertSet, so it would update nothing and be written as a conflict that "
        + "is ignored, silently, and a soft-deleted row in the way would be neither updated nor reported. "
        + "Nothing was executed. Pass the sets to apply (UpsertSet.Inserted, Increment or Now), "
        + "or use OnConflict.DoNothing.";

    private const string OrderNullsReason =
        "B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders "
        + "stated no NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and "
        + "OrderBy(...).Take(n) read different rows; every plain order term is now NULLS LAST, in both directions "
        + "and on both providers. Nothing else on the line moves.";

    private static Deviation OrderNulls(string name, string before, string after) =>
        new(name, DeviationKind.Changed, before, after, OrderNullsReason);

    private const string BackslashReason =
        "B7 follow-up (ORML-47): with standard_conforming_strings off a backslash in a quote-doubled string "
        + "escapes the next character and can end the literal early; PostgreSQL now writes a string that "
        + "holds a backslash as an escape string E'...' with the backslash and the quote doubled, which means "
        + "the same text under either setting. A string without a backslash, and every SQLite string, is unchanged.";

    private const string EnumReason =
        "B10 (ORML-11): a range (<, <=, >, >=) or an ordering over an enum column compared the stored member "
        + "NAMES as text, alphabetically, and answered with rows or an order the enum does not have; it is "
        + "refused by name before anything is rendered.";

    private const string EnumStored =
        "enums are stored as their member NAME, so the database would rank the names as text, "
        + "alphabetically and not by value, ";

    private static string EnumRange(string column, string op) =>
        "Cannot compare the enum column " + column + " with " + op + ": " + EnumStored
        + "and the range would silently select the wrong rows. Nothing was executed. "
        + "Compare with == or != (or Contains) against the members you mean, or read the rows and "
        + "compare the enum values in memory.";

    private static string EnumOrder(string column) =>
        "Cannot order by the enum column " + column + ": " + EnumStored
        + "and the rows would silently come back in an order the enum does not have. "
        + "Nothing was executed. Order by a column that holds the order you mean (an integer rank, "
        + "for example), or read the rows and sort the enum values in memory.";

    private static readonly string[] NotCovered =
    [
        "Execution against a PostgreSQL server: the goldens fix text and parameters, not what a server does with them. Server behaviour (jsonb operators, triggers, locks, advisory locks, `pg_input_is_valid`) stays proven only by the host's own Postgres suites.",
        "Provider-side behaviour of a real ADO.NET driver: array binding, parameter type inference, the pooled connection reset. The scripted provider returns what a case tells it to.",
        "Rows read back and mapped into entities: the cases map no rows except where a transcript needs one.",
        "Output that depends on the process rather than on the library: the actor setting other than the library default, `RowHistoryDdl.Preamble` as a process static, and timing.",
        "Compile-time behaviour of the generated code under AOT: generated sources are compiled against the runtime and read back, never published.",
        "The public API shape itself (signatures, accessibility): a later slice that changes it changes the cases and says so; only the text produced is pinned.",
        "Members listed under Excluded above, each with its reason.",
    ];
}
