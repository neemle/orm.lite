using System.Data;
using System.Linq.Expressions;

namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class RefusalCases
{
    private const string G = "refusals";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var tenant = CorpusWorld.Tenant;
        var ids = new List<Guid> { a };
        var none = new List<Guid>();
        string? nullText = null;
        Jsonb? nullDocument = null;
        var lazy = ids.Where(_ => true);
        var document = Jsonb.Parse("{}");
        var key = new MovedFieldKey("legacy_name");
        var emptyList = new List<string>();
        var nullElement = new List<string> { "ok", null! };
        Row<CorpusNote> NoteRow() => new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Title, "t");

        run.Rec.Refusal(G, "update-without-a-set", () => run.Q<CorpusNote>().Where(n => n.Id == a).RenderUpdate());
        run.Rec.Refusal(G, "update-without-a-where", () => run.Q<CorpusNote>().Set(n => n.Rank, 1).RenderUpdate());
        run.Rec.Refusal(G, "delete-without-a-where", () => run.Q<CorpusNote>().RenderDelete());
        run.Rec.Refusal(G, "soft-delete-without-a-where", () => run.Q<CorpusTask>().RenderDelete());
        run.Rec.Refusal(G, "purge-without-a-where", () => run.Q<CorpusTask>().RenderPurge());
        run.Rec.Refusal(G, "update-returning-without-a-set", () => run.Q<CorpusNote>().Where(n => n.Id == a).RenderUpdateReturning([n => n.Id]));
        run.Rec.Refusal(G, "update-returning-without-a-where", () => run.Q<CorpusNote>().Set(n => n.Rank, 1).RenderUpdateReturning([n => n.Id]));
        run.Rec.Refusal(G, "delete-returning-without-a-where", () => run.Q<CorpusNote>().RenderDeleteReturning([n => n.Id]));
        run.Rec.Refusal(G, "returning-an-unregistered-column", () => run.Q<CorpusPartial>().Where(p => p.Id == a).RenderDeleteReturning([p => p.Hidden]));
        run.Rec.Refusal(G, "returning-a-computed-lambda", () => run.Q<CorpusNote>().Where(n => n.Id == a).RenderDeleteReturning([n => n.Id, n => n.Rank + 0]));
        run.Rec.Refusal(G, "returning-a-lambda-that-is-not-a-column", () => run.Q<CorpusNote>().Where(n => n.Id == a).RenderDeleteReturning([n => 5]));

        run.Rec.Refusal(G, "purge-by-key-without-ids", () => run.Q<CorpusTask>().RenderPurge(none, tenant));
        run.Rec.Refusal(G, "purge-by-key-null-ids", () => run.Q<CorpusTask>().RenderPurge(null!, tenant));
        run.Rec.Refusal(G, "purge-by-key-on-an-entity-that-is-not-soft", () => run.Q<CorpusNote>().RenderPurge(ids, tenant));
        run.Rec.Refusal(G, "purge-by-key-without-a-key-column", () => run.Q<CorpusNoKey>().RenderPurge(ids, tenant));
        run.Rec.Refusal(G, "purge-by-key-tenant-entity-without-tenant", () => run.Q<CorpusTask>().RenderPurge(ids, null));
        run.Rec.Refusal(G, "purge-by-key-platform-entity-with-tenant", () => run.Q<CorpusPlatformItem>().RenderPurge(ids, tenant));
        run.Rec.Refusal(G, "restore-on-an-entity-that-is-not-soft", () => run.Q<CorpusNote>().RenderRestore(ids, tenant));
        run.Rec.Refusal(G, "restore-without-a-key-column", () => run.Q<CorpusNoKey>().RenderRestore(ids, tenant));
        run.Rec.Refusal(G, "restore-null-ids", () => run.Q<CorpusTask>().RenderRestore(null!, tenant));
        run.Rec.Refusal(G, "restore-tenant-entity-without-tenant", () => run.Q<CorpusTask>().RenderRestore(ids, null));
        run.Rec.Refusal(G, "restore-platform-entity-with-tenant", () => run.Q<CorpusPlatformItem>().RenderRestore(ids, tenant));

        run.Rec.Refusal(G, "upsert-with-an-undefined-conflict-action", () => run.Q<CorpusNote>().RenderUpsert(NoteRow(), [n => n.Id], (OnConflict)7));
        run.Rec.Refusal(G, "upsert-update-without-a-target", () => run.Q<CorpusNote>().RenderUpsert(NoteRow(), [], OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Title)]));
        run.Rec.Refusal(G, "upsert-inserted-column-not-in-the-row", () => run.Q<CorpusNote>().RenderUpsert(NoteRow(), [n => n.Id], OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Rank)]));
        run.Rec.Refusal(G, "upsert-null-row", () => run.Q<CorpusNote>().RenderUpsert(null!, [n => n.Id], OnConflict.DoNothing));
        run.Rec.Refusal(G, "upsert-target-lambda-is-not-a-column", () => run.Q<CorpusNote>().RenderUpsert(NoteRow(), [n => 5], OnConflict.DoNothing));
        run.Rec.Refusal(G, "upsert-returning-an-unregistered-column", () => run.Q<CorpusPartial>().RenderUpsert(new Row<CorpusPartial>().With(p => p.Id, a), [p => p.Id], OnConflict.DoNothing, null, [p => p.Hidden]));
        run.Rec.Refusal(G, "row-with-a-null-column", () => new Row<CorpusNote>().With(null!, 1));
        run.Rec.Refusal(G, "row-with-a-null-clock-column", () => new Row<CorpusNote>().WithNow(null!));
        run.Rec.Refusal(G, "upsert-set-with-a-null-column", () => UpsertSet<CorpusNote>.Inserted(null!));

        run.Rec.Refusal(G, "insert-many-without-rows", () => run.Q<CorpusNote>().RenderInsertMany([]));
        run.Rec.Refusal(G, "insert-many-null-rows", () => run.Q<CorpusNote>().RenderInsertMany(null!));
        run.Rec.Refusal(G, "insert-many-rows-with-different-column-counts", () => run.Q<CorpusNote>().RenderInsertMany([NoteRow(), new Row<CorpusNote>().With(n => n.Id, a)]));
        run.Rec.Refusal(G, "insert-many-rows-with-different-columns", () => run.Q<CorpusNote>().RenderInsertMany([NoteRow(), new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Rank, 1)]));
        run.Rec.Refusal(G, "insert-many-rows-with-different-clock-columns", () => run.Q<CorpusNote>().RenderInsertMany([NoteRow().WithNow(n => n.CreatedAt), NoteRow()]));

        run.Rec.Refusal(G, "project-without-columns", () => run.Q<CorpusNote>().RenderProject([]));
        run.Rec.Refusal(G, "projection-without-terms", () => run.Q<CorpusNote>().RenderProjection());
        run.Rec.Refusal(G, "aggregates-without-terms", () => run.Q<CorpusNote>().RenderAggregates());
        run.Rec.Refusal(G, "where-in-builder-returns-another-query", () => run.Q<CorpusNote>().WhereIn<CorpusTag>(n => n.Id, _ => run.Q<CorpusTag>(), t => t.NoteId));
        run.Rec.Refusal(G, "query-for-an-unregistered-type", () => run.Q<CorpusUnregistered>());
        run.Rec.Refusal(G, "query-on-a-null-unit-of-work", () => run.Q<CorpusNote>().On(null!));

        run.Rec.Refusal(G, "predicate-unsupported-binary", () => run.Q<CorpusNote>().Where(n => n.Active ^ n.Active));
        run.Rec.Refusal(G, "predicate-unsupported-call", () => run.Q<CorpusNote>().Where(n => n.Title.IsNormalized()));
        run.Rec.Refusal(G, "predicate-contains-of-a-character", () => run.Q<CorpusNote>().Where(n => n.Title.Contains('a')));
        run.Rec.Refusal(G, "predicate-any-of-lazy-sequence", () => run.Q<CorpusNote>().Where(n => Db.AnyOf(n.Id, lazy)));
        run.Rec.Refusal(G, "predicate-any-of-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.AnyOf(a, ids)));
        run.Rec.Refusal(G, "predicate-null-safe-equals-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.NullSafeEquals(a, a)));
        run.Rec.Refusal(G, "predicate-equals-ignore-case-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.EqualsIgnoreCase("lit", n.Title)));
        run.Rec.Refusal(G, "predicate-json-key-equals-null-key", () => run.Q<CorpusNote>().Where(n => Db.JsonKeyEquals(n.Payload, nullText!, "v", JsonValueOrder.Text)));
        run.Rec.Refusal(G, "predicate-json-key-equals-null-value", () => run.Q<CorpusNote>().Where(n => Db.JsonKeyEquals(n.Payload, "k", nullText!, JsonValueOrder.Text)));
        run.Rec.Refusal(G, "predicate-json-key-equals-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.JsonKeyEquals(document, "k", "v", JsonValueOrder.Text)));
        run.Rec.Refusal(G, "predicate-json-has-key-null-key", () => run.Q<CorpusNote>().Where(n => Db.JsonHasKey(n.Payload, nullText!)));
        run.Rec.Refusal(G, "predicate-json-has-key-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.JsonHasKey(document, "k")));
        run.Rec.Refusal(G, "predicate-json-array-contains-string-null-value", () => run.Q<CorpusNote>().Where(n => Db.JsonArrayContainsString(n.Payload, "k", nullText!)));
        run.Rec.Refusal(G, "predicate-json-array-contains-string-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.JsonArrayContainsString(document, "k", "v")));
        run.Rec.Refusal(G, "predicate-json-array-contains-any-empty", () => run.Q<CorpusNote>().Where(n => Db.JsonArrayContainsAny(n.Payload, "k", emptyList)));
        run.Rec.Refusal(G, "predicate-json-array-contains-any-null-element", () => run.Q<CorpusNote>().Where(n => Db.JsonArrayContainsAny(n.Payload, "k", nullElement)));
        run.Rec.Refusal(G, "predicate-json-array-contains-any-first-argument-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.JsonArrayContainsAny(document, "k", emptyList)));
        run.Rec.Refusal(G, "predicate-moved-field-equals-null-value", () => run.Q<CorpusNote>().Where(n => Db.MovedFieldEquals<string?>(n.Meta, key, nullText)));
        run.Rec.Refusal(G, "predicate-moved-field-contains-null-term", () => run.Q<CorpusNote>().Where(n => Db.MovedFieldContains(n.Meta, key, nullText!)));
        run.Rec.Refusal(G, "predicate-moved-field-document-not-a-column", () => run.Q<CorpusNote>().Where(n => Db.MovedFieldIsNull(document, key)));
        run.Rec.Refusal(G, "predicate-marker-executed-directly", () => Db.AnyOf(1, [1]));
        run.Rec.Refusal(G, "predicate-exists-marker-executed-directly", () => Db.Exists<CorpusNote>(n => true));
        run.Rec.Refusal(G, "predicate-now-marker-executed-directly", () => Db.Now());

        run.Rec.Refusal(G, "json-merge-after-a-set-of-the-same-column", () => run.Q<CorpusNote>().Set(n => n.Meta, document).SetJsonMerge(n => n.Meta, document));
        run.Rec.Refusal(G, "json-set-after-a-merge", () => run.Q<CorpusNote>().SetJsonMerge(n => n.Meta, document).Set(n => n.Title, "t"));
        run.Rec.Refusal(G, "json-second-merge", () => run.Q<CorpusNote>().SetJsonMerge(n => n.Meta, document).SetJsonMerge(n => n.Payload, document));
        run.Rec.Refusal(G, "json-merge-null-document", () => run.Q<CorpusNote>().SetJsonMerge(null!, document));
        run.Rec.Refusal(G, "json-merge-null-patch", () => run.Q<CorpusNote>().SetJsonMerge(n => n.Meta, nullDocument!));
        run.Rec.Refusal(G, "json-remove-then-merge-without-keys", () => run.Q<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, [], [], document));
        run.Rec.Refusal(G, "json-remove-then-merge-duplicate-cleared-key", () => run.Q<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, [key, key], [], document));
        run.Rec.Refusal(G, "json-remove-then-merge-key-cleared-and-set", () => run.Q<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, [key], [key], document));
        run.Rec.Refusal(G, "json-remove-then-merge-null-cleared-keys", () => run.Q<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, null!, [key], document));
        run.Rec.Refusal(G, "json-remove-then-merge-null-set-keys", () => run.Q<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, [key], null!, document));
        run.Rec.Refusal(G, "json-remove-then-merge-null-patch", () => run.Q<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, [key], [], nullDocument!));
        run.Rec.Refusal(G, "json-member-increment-null-document", () => run.Q<CorpusNote>().SetJsonMemberIncrement(null!, key, 1));
        run.Rec.Refusal(G, "json-order-of-null-ordering", () => run.Q<CorpusNote>().OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, null!));
        run.Rec.Refusal(G, "json-order-of-null-arranged-option", () => run.Q<CorpusNote>().OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, new JsonKeyOrdering(new JsonKey("k"), false, false, JsonValueOrder.Text, [null!])));
        run.Rec.Refusal(G, "json-order-null-arrangement", () => new JsonKeyOrdering(new JsonKey("k"), false, false, JsonValueOrder.Text, null!));
        run.Rec.Refusal(G, "json-key-null", () => new JsonKey(null!));
        run.Rec.Refusal(G, "json-key-default", () => _ = default(JsonKey).Name);
        run.Rec.Refusal(G, "moved-field-key-null", () => new MovedFieldKey(null!));
        run.Rec.Refusal(G, "moved-field-key-empty", () => new MovedFieldKey(""));
        run.Rec.Refusal(G, "moved-field-key-uppercase", () => new MovedFieldKey("Legacy"));
        run.Rec.Refusal(G, "moved-field-key-with-a-quote", () => new MovedFieldKey("a'b"));
        run.Rec.Refusal(G, "moved-field-key-default", () => _ = default(MovedFieldKey).Name);
        run.Rec.Refusal(G, "jsonb-parse-invalid", () => Jsonb.Parse("{"));
        run.Rec.Refusal(G, "jsonb-parse-null", () => Jsonb.Parse(null!));
        run.Rec.Refusal(G, "jsonb-from-storage-null", () => Jsonb.FromStorage(null!));

        run.Rec.Refusal(G, "capability-upsert", () => run.QNoCap<CorpusNote>().RenderUpsert(NoteRow(), [n => n.Id], OnConflict.DoNothing));
        run.Rec.Refusal(G, "capability-update-returning", () => run.QNoCap<CorpusNote>().Set(n => n.Rank, 1).Where(n => n.Id == a).RenderUpdateReturning([n => n.Id]));
        run.Rec.Refusal(G, "capability-delete-returning", () => run.QNoCap<CorpusNote>().Where(n => n.Id == a).RenderDeleteReturning([n => n.Id]));
        run.Rec.Refusal(G, "capability-array-parameter", () => run.QNoCap<CorpusNote>().Where(n => Db.AnyOf(n.Id, ids)));
        run.Rec.Refusal(G, "capability-null-safe-equality", () => run.QNoCap<CorpusNote>().Where(n => Db.NullSafeEquals(n.OwnerId, a)));
        run.Rec.Refusal(G, "capability-row-lock", () => run.QNoCap<CorpusNote>().ForUpdate());
        run.Rec.Refusal(G, "capability-key-preserving-row-lock", () => run.QNoCap<CorpusNote>().ForNoKeyUpdate());
        run.Rec.Refusal(G, "capability-aggregate-filter", () => run.QNoCap<CorpusNote>().AggregateCountWhere(n => n.Active));
        run.Rec.Refusal(G, "capability-interval-arithmetic", () => run.QNoCap<CorpusNote>().SetNowPlusMinutes(n => n.ExpiresAt, 5));
        run.Rec.Refusal(G, "capability-json-filter-key-equals", () => run.QNoCap<CorpusNote>().Where(n => Db.JsonKeyEquals(n.Payload, "k", "v", JsonValueOrder.Text)));
        run.Rec.Refusal(G, "capability-json-filter-has-key", () => run.QNoCap<CorpusNote>().Where(n => Db.JsonHasKey(n.Payload, "k")));
        run.Rec.Refusal(G, "capability-json-filter-array-contains-string", () => run.QNoCap<CorpusNote>().Where(n => Db.JsonArrayContainsString(n.Payload, "k", "v")));
        run.Rec.Refusal(G, "capability-json-filter-array-contains-any", () => run.QNoCap<CorpusNote>().Where(n => Db.JsonArrayContainsAny(n.Payload, "k", emptyList)));
        run.Rec.Refusal(G, "capability-json-moved-field", () => run.QNoCap<CorpusNote>().Where(n => Db.MovedFieldIsNull(n.Meta, key)));
        run.Rec.Refusal(G, "capability-json-bind-document-in-set", () => run.QNoCap<CorpusNote>().Set(n => n.Payload, document));
        run.Rec.Refusal(G, "capability-json-bind-document-in-compare", () => run.QNoCap<CorpusNote>().Where(n => n.Payload == document));
        run.Rec.Refusal(G, "capability-json-order-by-key", () => run.QNoCap<CorpusNote>().OrderByJsonKey(n => n.Payload, new JsonKey("k"), false, false));
        run.Rec.Refusal(G, "capability-json-order-by-key-of-other-table", () => run.QNoCap<CorpusNote>().OrderByJsonKeyOf<CorpusTag>((n, t) => t.NoteId == n.Id, t => t.Doc, new JsonKeyOrdering(new JsonKey("k"), false, false, JsonValueOrder.Text, [])));
        run.Rec.Refusal(G, "capability-json-order-by-moved-field", () => run.QNoCap<CorpusNote>().OrderByMovedFieldNumberOrEpochMillis(n => n.Meta, key, n => n.CreatedAt));
        run.Rec.Refusal(G, "capability-json-merge", () => run.QNoCap<CorpusNote>().SetJsonMerge(n => n.Meta, document));
        run.Rec.Refusal(G, "capability-json-remove-then-merge", () => run.QNoCap<CorpusNote>().SetJsonRemoveThenMerge(n => n.Meta, [key], [], document));
        run.Rec.Refusal(G, "capability-json-member-increment", () => run.QNoCap<CorpusNote>().SetJsonMemberIncrement(n => n.Meta, key, 1));
        run.Rec.Refusal(G, "capability-json-upsert-binds-document", () => run.QNoCap<CorpusNote>().RenderUpsert(new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Payload, document), [n => n.Id], OnConflict.DoNothing));

        run.Rec.Refusal(G, "dialect-json-value-order-without-a-sql-type", () => new PgDialect().JsonKeyComparableExpression("\"c\"", "@p0", (JsonValueOrder)9));
        run.Rec.Refusal(G, "dialect-rank-without-options", () => new PgDialect().JsonKeyRankExpression("\"c\"", "@p0", []));

        run.Rec.Refusal(G, "enum-read-of-an-unknown-stored-value", () => ReadEnum("Bogus"));
        run.Rec.Refusal(G, "enum-read-of-a-blank-stored-value", () => ReadEnum("  "));
        run.Rec.Text(G, "enum-try-parse-exact-table", () =>
        {
            var lines = new List<string>();
            foreach (var stored in new string?[] { null, "", "  ", "Draft", " Active ", "active", "ARCHIVED", "Bogus", "7", "1" })
                lines.Add(CorpusFormat.Value(stored) + " -> " + (OrmEnum.TryParseExact<CorpusStatus>(stored, out var value) ? "parsed " + value : "refused"));
            return CorpusFormat.Lines(lines);
        });

        run.Rec.Refusal(G, "migration-without-a-down-override", () => new NoDownMigration().Down(new MigrationBuilder()));
        run.Rec.Refusal(G, "migration-builder-delete-data-without-values", () => new MigrationBuilder().DeleteData("t", "c"));
        run.Rec.Refusal(G, "migration-builder-insert-data-without-rows",
            () => new MigrationBuilder().InsertData("t", ["c"]));
        run.Rec.Refusal(G, "migration-builder-insert-data-without-columns",
            () => new MigrationBuilder().InsertData("t", Array.Empty<string>(), new object?[0]));
        run.Rec.Refusal(G, "migration-builder-delete-data-with-a-null-value",
            () => new MigrationBuilder().DeleteData("t", "c", "x", null));
        run.Rec.Refusal(G, "ddl-literal-non-finite-number",
            () => new MigrationBuilder().InsertData("t", ["c"], [double.NaN]));
        run.Rec.Refusal(G, "ddl-literal-unsupported-type",
            () => new MigrationBuilder().InsertData("t", ["c"], [TimeSpan.FromSeconds(1)]));
        var huge = long.MaxValue;
        run.Rec.Refusal(G, "where-closure-checked-overflow",
            () => run.Q<CorpusNote>().Where(n => n.Rank == checked((int)huge)));
        run.Rec.Refusal(G, "actor-setting-not-a-dotted-lowercase-pair", () => OrmLiteSettings.UseActorSetting("Bad Name"));
        run.Rec.Refusal(G, "actor-setting-with-a-long-part", () => OrmLiteSettings.UseActorSetting("abcdefghijklmnopqrstuvwxyzabcdefg.actor"));
        run.Rec.Refusal(G, "actor-setting-without-a-dot", () => OrmLiteSettings.UseActorSetting("nodot"));
        run.Rec.Refusal(G, "actor-setting-null", () => OrmLiteSettings.UseActorSetting(null!));
    }

    private sealed class NoDownMigration : Migration
    {
        public override void Up(MigrationBuilder builder) => builder.Raw("postgres", "SELECT 1");
    }

    private static void ReadEnum(string stored)
    {
        var table = new DataTable();
        table.Columns.Add("status", typeof(string));
        table.Columns.Add("id", typeof(string));
        table.Rows.Add(stored, "row-1");
        using var reader = table.CreateDataReader();
        reader.Read();
        OrmEnum.Read<CorpusStatus>(reader, 0, "corpus_notes", "status", "id");
    }
}
