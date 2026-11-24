namespace Neemle.Orm.Lite.Tests.Corpus;

// The approved deviations that the PostgreSQL lowering of ORML-30 (B43, B44, B48) moves in the corpus goldens.
// Texts are derived by reading the lowering and the SqlWriter spacing, not captured: the capture regenerates
// the goldens and INDEX.md, and this list only documents what moved and why.
internal static class CorpusLoweringDeviations
{
    private const string Msg =
        "Index name % is already held by another relation in the schema of table %: PostgreSQL index names are "
        + "schema-global, so creating it would be skipped and the table would stay without its index. "
        + "Rename the index.";

    private const string Transcripts =
        "migration-runner-fresh-database, migration-runner-partially-applied and "
        + "executed-schema-contributions-and-registry";

    private const string UpReason =
        "B44 (ORML-30): the Up block found the total index by its bare name through search_path, so a total index "
        + "of another table made it drop that table's index, and a live name held by another table made "
        + "CREATE ... IF NOT EXISTS skip while the total index was dropped; both indexes are now read through "
        + "pg_index.indrelid of the table, the total index is dropped by oid, and a live name held by another "
        + "relation is refused.";

    private const string DownReason =
        "B44 (ORML-30): the Down block read the live marker and dropped or released the live index by its bare "
        + "name, and recreated the total index with IF NOT EXISTS under a name another table may hold; both "
        + "indexes are now read through pg_index.indrelid of the table, the live index is released by oid, and a "
        + "total name held by another relation is refused.";

    private const string CompareOld =
        "IF regexp_replace(pg_get_indexdef(current_index), ' ON \\S+ USING ', ' USING ') <>";

    private static readonly (string Set, string[] Tables, (string Table, string Index)[] Indexes)[] Sets =
    [
        ("generator-demo", ["notes"], [("notes", "ix_notes_tenant_id_title_live")]),
        (
            "generator-corpus-entities",
            ["ce_accounts", "ce_everything", "ce_projects", "ce_tasks"],
            [
                ("ce_accounts", "ix_ce_accounts_code_live"), ("ce_accounts", "ix_ce_accounts_name"),
                ("ce_everything", "ix_ce_everything_searchable"), ("ce_everything", "ix_ce_everything_unique"),
                ("ce_projects", "ix_ce_projects_tenant_id_title_live"), ("ce_projects", "ix_ce_projects_title"),
                ("ce_tasks", "ix_ce_tasks_project_id_name"), ("ce_projects", "ix_projects_state"),
            ]),
        ("generator-golden-fixtures", ["fixture_notes"], [("fixture_notes", "ix_fixture_notes_title")]),
        (
            "generator-global-namespace",
            ["gn_orchards", "gn_trees"],
            [("gn_orchards", "ix_gn_orchards_name_live"), ("gn_trees", "ix_gn_trees_orchard_id_species")]),
    ];

    private static string Quoted(string table) => "'\"" + table + "\"'";

    private static string LookupOld(string index) => "current_index := to_regclass(" + Quoted(index) + ");";

    private static string LookupNew(string table, string index) =>
        "SELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid "
        + "WHERE i.indrelid = to_regclass(" + Quoted(table) + ") AND ic.relname = '" + index + "'; "
        + "IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = '" + index + "' "
        + "AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass("
        + Quoted(table) + "))) "
        + "THEN RAISE EXCEPTION '" + Msg + "', '" + index + "', '" + table + "'; END IF;";

    private static string CompareNew(string table) =>
        "IF replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.', "
        + "quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
        + "WHERE c.oid = to_regclass(" + Quoted(table) + ")), ' USING '), ' USING ') <>";

    private static CorpusIndex.Deviation Lookup(string set, (string Table, string Index)[] indexes) => new(
        set + "/" + Transcripts + " (drift lookup and name refusal, one per index)",
        CorpusIndex.DeviationKind.Changed,
        string.Join("\n", indexes.Select(i => LookupOld(i.Index))),
        string.Join("\n", indexes.Select(i => LookupNew(i.Table, i.Index))),
        "B44 (ORML-30): the drift block found an index by its bare name through search_path, so two tables that "
        + "declared one index name skipped each other's CREATE and dropped each other's index; it now reads the "
        + "index through pg_index.indrelid of its own table and refuses a name another relation of the schema "
        + "holds. Every drift block of the set changes this way; the fragments are listed per index.");

    private static CorpusIndex.Deviation Compare(string set, string[] tables) => new(
        set + "/" + Transcripts + " (drift compare, one per table)",
        CorpusIndex.DeviationKind.Changed,
        string.Join("\n", tables.Select(_ => CompareOld)),
        string.Join("\n", tables.Select(CompareNew)),
        "B48 (ORML-30): the current definition was stripped of its table by ' ON \\S+ USING ', which cannot span "
        + "whitespace, so a table name with whitespace always differed and its index was rebuilt on every run; the "
        + "exact schema-qualified table text is removed instead. The probe side keeps the pattern: its table name "
        + "is the fixed probe name. The fragment is listed once per table of the set.");

    private static CorpusIndex.Deviation ExistingTables() => new(
        "executed/soft-trash-existing-tables-are-cached-per-data-source, soft-trash-list-with-more-rows, "
        + "soft-trash-list-with-a-project-filter-and-offset, soft-trash-list-with-no-listable-entry-runs-no-union "
        + "and soft-trash-list-skips-missing-tables-and-keyless-entries",
        CorpusIndex.DeviationKind.Changed,
        "SELECT t FROM unnest(@tables::text[]) AS t WHERE to_regclass(quote_ident(t)) IS NOT NULL",
        "SELECT t FROM unnest(@tables::text[]) AS t WHERE EXISTS (SELECT 1 FROM pg_class c "
        + "WHERE c.oid = to_regclass(quote_ident(t)) AND c.relkind IN ('p', 'r'))",
        "B48 (ORML-30): to_regclass also resolves a view, a materialized view, a foreign table, a sequence and an "
        + "index, so the existing-tables lookup took them for tables; it now accepts only an ordinary or a "
        + "partitioned table, which also drops foreign tables and materialized views from the answer. "
        + "Parameters are unchanged.");

    private static readonly CorpusIndex.Deviation UpPlainKey = new(
        "ddl-helpers/live-only-up-plain-key",
        CorpusIndex.DeviationKind.Changed,
        "DO $lc$ DECLARE owner_constraint name; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF "
        + "to_regclass('\"uq_samples_name\"') IS NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "
        + "\"ix_samples_name_live\" ON \"samples\" (\"name\") WHERE \"deleted_at\" IS NULL; COMMENT ON INDEX "
        + "\"ix_samples_name_live\" IS '0200_live_only'; SELECT c.conname INTO owner_constraint FROM "
        + "pg_constraint c WHERE c.conrelid = to_regclass('\"samples\"') AND c.conindid = "
        + "to_regclass('\"uq_samples_name\"') AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN DROP "
        + "INDEX \"uq_samples_name\"; ELSE EXECUTE format('ALTER TABLE \"samples\" DROP CONSTRAINT %I', "
        + "owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS \"ix_samples_name_live\" ON "
        + "\"samples\" (\"name\") WHERE \"deleted_at\" IS NULL; END IF; END $lc$",
        "DO $lc$ DECLARE owner_constraint name; total_index oid; live_index oid; BEGIN PERFORM "
        + "set_config('lock_timeout', '5s', true); SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class "
        + "ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = "
        + "'uq_samples_name'; SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = "
        + "i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = 'ix_samples_name_live'; IF "
        + "live_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_samples_name_live' AND "
        + "x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('\"samples\"'))) "
        + "THEN RAISE EXCEPTION '" + Msg + "', 'ix_samples_name_live', 'samples'; END IF; IF total_index IS NOT "
        + "NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS \"ix_samples_name_live\" ON \"samples\" (\"name\") WHERE "
        + "\"deleted_at\" IS NULL; COMMENT ON INDEX \"ix_samples_name_live\" IS '0200_live_only'; SELECT "
        + "c.conname INTO owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('\"samples\"') AND "
        + "c.conindid = total_index AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN EXECUTE 'DROP "
        + "INDEX ' || total_index::regclass; ELSE EXECUTE format('ALTER TABLE \"samples\" DROP CONSTRAINT %I', "
        + "owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS \"ix_samples_name_live\" ON "
        + "\"samples\" (\"name\") WHERE \"deleted_at\" IS NULL; END IF; END $lc$",
        UpReason);

    private static readonly CorpusIndex.Deviation UpLoweredKey = new(
        "ddl-helpers/live-only-up-lowered-composite-key-undeclared-live-index",
        CorpusIndex.DeviationKind.Changed,
        "DO $lc$ DECLARE owner_constraint name; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF "
        + "to_regclass('\"uq_samples_name\"') IS NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "
        + "\"ix_samples_name_live\" ON \"samples\" (lower(\"name\"), \"scope\") WHERE \"deleted_at\" IS NULL; "
        + "COMMENT ON INDEX \"ix_samples_name_live\" IS '0200_live_only'; SELECT c.conname INTO owner_constraint "
        + "FROM pg_constraint c WHERE c.conrelid = to_regclass('\"samples\"') AND c.conindid = "
        + "to_regclass('\"uq_samples_name\"') AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN DROP "
        + "INDEX \"uq_samples_name\"; ELSE EXECUTE format('ALTER TABLE \"samples\" DROP CONSTRAINT %I', "
        + "owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS \"ix_samples_name_live\" ON \"samples\" "
        + "(lower(\"name\"), \"scope\") WHERE \"deleted_at\" IS NULL; END IF; END $lc$",
        "DO $lc$ DECLARE owner_constraint name; total_index oid; live_index oid; BEGIN PERFORM "
        + "set_config('lock_timeout', '5s', true); SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class ic "
        + "ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = "
        + "'uq_samples_name'; SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = "
        + "i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = 'ix_samples_name_live'; IF "
        + "live_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_samples_name_live' AND "
        + "x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('\"samples\"'))) THEN "
        + "RAISE EXCEPTION '" + Msg + "', 'ix_samples_name_live', 'samples'; END IF; IF total_index IS NOT NULL THEN "
        + "CREATE UNIQUE INDEX IF NOT EXISTS \"ix_samples_name_live\" ON \"samples\" (lower(\"name\"), \"scope\") "
        + "WHERE \"deleted_at\" IS NULL; COMMENT ON INDEX \"ix_samples_name_live\" IS '0200_live_only'; SELECT "
        + "c.conname INTO owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('\"samples\"') AND "
        + "c.conindid = total_index AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN EXECUTE 'DROP "
        + "INDEX ' || total_index::regclass; ELSE EXECUTE format('ALTER TABLE \"samples\" DROP CONSTRAINT %I', "
        + "owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS \"ix_samples_name_live\" ON \"samples\" "
        + "(lower(\"name\"), \"scope\") WHERE \"deleted_at\" IS NULL; END IF; END $lc$",
        UpReason);

    private static readonly CorpusIndex.Deviation DownPlainKey = new(
        "ddl-helpers/live-only-down-plain-key-declared-live-index",
        CorpusIndex.DeviationKind.Changed,
        "DO $lc$ DECLARE duplicate_key text; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF "
        + "coalesce(obj_description(to_regclass('\"ix_samples_name_live\"'), 'pg_class'), '') = '0200_live_only' "
        + "THEN SELECT concat_ws(', ', \"name\"::text) INTO duplicate_key FROM \"samples\" WHERE \"name\" IS NOT "
        + "NULL GROUP BY \"name\" HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION "
        + "'cannot recreate unique index uq_samples_name on samples (name): duplicate key % exists, nothing was "
        + "deleted', quote_literal(duplicate_key); END IF; CREATE UNIQUE INDEX IF NOT EXISTS \"uq_samples_name\" "
        + "ON \"samples\" (\"name\"); COMMENT ON INDEX \"ix_samples_name_live\" IS NULL; END IF; END $lc$",
        "DO $lc$ DECLARE duplicate_key text; total_index oid; live_index oid; BEGIN PERFORM "
        + "set_config('lock_timeout', '5s', true); SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class "
        + "ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = "
        + "'ix_samples_name_live'; SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class ic ON ic.oid = "
        + "i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = 'uq_samples_name'; IF "
        + "coalesce(obj_description(live_index, 'pg_class'), '') = '0200_live_only' THEN SELECT concat_ws(', ', "
        + "\"name\"::text) INTO duplicate_key FROM \"samples\" WHERE \"name\" IS NOT NULL GROUP BY \"name\" "
        + "HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot recreate "
        + "unique index uq_samples_name on samples (name): duplicate key % exists, nothing was deleted', "
        + "quote_literal(duplicate_key); END IF; IF total_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x "
        + "WHERE x.relname = 'uq_samples_name' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE "
        + "t.oid = to_regclass('\"samples\"'))) THEN RAISE EXCEPTION '" + Msg + "', 'uq_samples_name', "
        + "'samples'; END IF; CREATE UNIQUE INDEX IF NOT EXISTS \"uq_samples_name\" ON \"samples\" (\"name\"); "
        + "EXECUTE format('COMMENT ON INDEX %s IS NULL', live_index::regclass); END IF; END $lc$",
        DownReason);

    private static readonly CorpusIndex.Deviation DownLoweredKey = new(
        "ddl-helpers/live-only-down-lowered-composite-key-undeclared-live-index",
        CorpusIndex.DeviationKind.Changed,
        "DO $lc$ DECLARE duplicate_key text; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF "
        + "coalesce(obj_description(to_regclass('\"ix_samples_name_live\"'), 'pg_class'), '') = '0200_live_only' "
        + "THEN SELECT concat_ws(', ', lower(\"name\")::text, \"scope\"::text) INTO duplicate_key FROM "
        + "\"samples\" WHERE \"name\" IS NOT NULL AND \"scope\" IS NOT NULL GROUP BY lower(\"name\"), \"scope\" "
        + "HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot recreate "
        + "unique index uq_samples_name on samples (lower(name), scope): duplicate key % exists, nothing was "
        + "deleted', quote_literal(duplicate_key); END IF; CREATE UNIQUE INDEX IF NOT EXISTS \"uq_samples_name\" "
        + "ON \"samples\" (lower(\"name\"), \"scope\"); DROP INDEX \"ix_samples_name_live\"; END IF; END $lc$",
        "DO $lc$ DECLARE duplicate_key text; total_index oid; live_index oid; BEGIN PERFORM "
        + "set_config('lock_timeout', '5s', true); SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class "
        + "ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = "
        + "'ix_samples_name_live'; SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class ic ON ic.oid = "
        + "i.indexrelid WHERE i.indrelid = to_regclass('\"samples\"') AND ic.relname = 'uq_samples_name'; IF "
        + "coalesce(obj_description(live_index, 'pg_class'), '') = '0200_live_only' THEN SELECT concat_ws(', ', "
        + "lower(\"name\")::text, \"scope\"::text) INTO duplicate_key FROM \"samples\" WHERE \"name\" IS NOT "
        + "NULL AND \"scope\" IS NOT NULL GROUP BY lower(\"name\"), \"scope\" HAVING count(*) > 1 LIMIT 1; IF "
        + "duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot recreate unique index uq_samples_name on "
        + "samples (lower(name), scope): duplicate key % exists, nothing was deleted', "
        + "quote_literal(duplicate_key); END IF; IF total_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x "
        + "WHERE x.relname = 'uq_samples_name' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE "
        + "t.oid = to_regclass('\"samples\"'))) THEN RAISE EXCEPTION '" + Msg + "', 'uq_samples_name', "
        + "'samples'; END IF; CREATE UNIQUE INDEX IF NOT EXISTS \"uq_samples_name\" ON \"samples\" "
        + "(lower(\"name\"), \"scope\"); EXECUTE 'DROP INDEX ' || live_index::regclass; END IF; END $lc$",
        DownReason);

    private static readonly CorpusIndex.Deviation GeneratedUp = new(
        "generator-corpus-entities/" + Transcripts + " (live-only Up block)",
        CorpusIndex.DeviationKind.Changed,
        "DO $lc$ DECLARE owner_constraint name; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF "
        + "to_regclass('\"uq_projects_title\"') IS NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "
        + "\"ix_projects_title_live\" ON \"ce_projects\" (\"tenant_id\", lower(\"title\")) WHERE \"deleted_at\" "
        + "IS NULL; COMMENT ON INDEX \"ix_projects_title_live\" IS '0400_helper_blocks'; SELECT c.conname INTO "
        + "owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('\"ce_projects\"') AND c.conindid "
        + "= to_regclass('\"uq_projects_title\"') AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN "
        + "DROP INDEX \"uq_projects_title\"; ELSE EXECUTE format('ALTER TABLE \"ce_projects\" DROP CONSTRAINT "
        + "%I', owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS \"ix_projects_title_live\" ON "
        + "\"ce_projects\" (\"tenant_id\", lower(\"title\")) WHERE \"deleted_at\" IS NULL; END IF; END $lc$",
        "DO $lc$ DECLARE owner_constraint name; total_index oid; live_index oid; BEGIN PERFORM "
        + "set_config('lock_timeout', '5s', true); SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class "
        + "ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('\"ce_projects\"') AND ic.relname = "
        + "'uq_projects_title'; SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = "
        + "i.indexrelid WHERE i.indrelid = to_regclass('\"ce_projects\"') AND ic.relname = "
        + "'ix_projects_title_live'; IF live_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = "
        + "'ix_projects_title_live' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = "
        + "to_regclass('\"ce_projects\"'))) THEN RAISE EXCEPTION '" + Msg + "', 'ix_projects_title_live', "
        + "'ce_projects'; END IF; IF total_index IS NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "
        + "\"ix_projects_title_live\" ON \"ce_projects\" (\"tenant_id\", lower(\"title\")) WHERE \"deleted_at\" "
        + "IS NULL; COMMENT ON INDEX \"ix_projects_title_live\" IS '0400_helper_blocks'; SELECT c.conname INTO "
        + "owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('\"ce_projects\"') AND c.conindid "
        + "= total_index AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN EXECUTE 'DROP INDEX ' || "
        + "total_index::regclass; ELSE EXECUTE format('ALTER TABLE \"ce_projects\" DROP CONSTRAINT %I', "
        + "owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS \"ix_projects_title_live\" ON "
        + "\"ce_projects\" (\"tenant_id\", lower(\"title\")) WHERE \"deleted_at\" IS NULL; END IF; END $lc$",
        UpReason);

    private static readonly CorpusIndex.Deviation GeneratedDown = new(
        "generator-corpus-entities/executed-schema-contributions-and-registry (live-only Down block)",
        CorpusIndex.DeviationKind.Changed,
        "DO $lc$ DECLARE duplicate_key text; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF "
        + "coalesce(obj_description(to_regclass('\"ix_projects_title_live\"'), 'pg_class'), '') = "
        + "'0400_helper_blocks' THEN SELECT concat_ws(', ', \"tenant_id\"::text, lower(\"title\")::text) INTO "
        + "duplicate_key FROM \"ce_projects\" WHERE \"tenant_id\" IS NOT NULL AND \"title\" IS NOT NULL GROUP BY "
        + "\"tenant_id\", lower(\"title\") HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE "
        + "EXCEPTION 'cannot recreate unique index uq_projects_title on ce_projects (tenant_id, lower(title)): "
        + "duplicate key % exists, nothing was deleted', quote_literal(duplicate_key); END IF; CREATE UNIQUE INDEX "
        + "IF NOT EXISTS \"uq_projects_title\" ON \"ce_projects\" (\"tenant_id\", lower(\"title\")); COMMENT ON "
        + "INDEX \"ix_projects_title_live\" IS NULL; END IF; END $lc$",
        "DO $lc$ DECLARE duplicate_key text; total_index oid; live_index oid; BEGIN PERFORM "
        + "set_config('lock_timeout', '5s', true); SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON "
        + "ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('\"ce_projects\"') AND ic.relname = "
        + "'ix_projects_title_live'; SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class ic ON ic.oid = "
        + "i.indexrelid WHERE i.indrelid = to_regclass('\"ce_projects\"') AND ic.relname = 'uq_projects_title'; IF "
        + "coalesce(obj_description(live_index, 'pg_class'), '') = '0400_helper_blocks' THEN SELECT concat_ws(', ', "
        + "\"tenant_id\"::text, lower(\"title\")::text) INTO duplicate_key FROM \"ce_projects\" WHERE \"tenant_id\" "
        + "IS NOT NULL AND \"title\" IS NOT NULL GROUP BY \"tenant_id\", lower(\"title\") HAVING count(*) > 1 LIMIT "
        + "1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot recreate unique index uq_projects_title on "
        + "ce_projects (tenant_id, lower(title)): duplicate key % exists, nothing was deleted', "
        + "quote_literal(duplicate_key); END IF; IF total_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE "
        + "x.relname = 'uq_projects_title' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = "
        + "to_regclass('\"ce_projects\"'))) THEN RAISE EXCEPTION '" + Msg + "', 'uq_projects_title', 'ce_projects'; "
        + "END IF; CREATE UNIQUE INDEX IF NOT EXISTS \"uq_projects_title\" ON \"ce_projects\" (\"tenant_id\", "
        + "lower(\"title\")); EXECUTE format('COMMENT ON INDEX %s IS NULL', live_index::regclass); END IF; END $lc$",
        DownReason);

    internal static readonly CorpusIndex.Deviation[] All =
    [
        .. Sets.SelectMany(set => new[] { Lookup(set.Set, set.Indexes), Compare(set.Set, set.Tables) }),
        UpPlainKey,
        UpLoweredKey,
        DownPlainKey,
        DownLoweredKey,
        GeneratedUp,
        GeneratedDown,
        ExistingTables(),
    ];
}
