# Characterization corpus of the PostgreSQL output

Every file under `Goldens/` is the live output of the library as it was before the redesign, recorded as text. Nothing here is hand-written: `scripts/CorpusCapture.cs` writes the goldens and this index, and `CorpusGoldenTests` compares the live output with them on every test run. The goldens were recorded from `src/` at base commit `0adff7e`, before the redesign; every later slice is proven against them and does not re-record them.

## Use

- Refresh: `dotnet run scripts/CorpusCapture.cs -- capture` (inside the SDK container).
- Verify without writing: `dotnet run scripts/CorpusCapture.cs -- check`.
- Capture into another directory, for a determinism comparison: `dotnet run scripts/CorpusCapture.cs -- capture --out <directory>`.
- A later slice that changes how text is produced keeps these goldens and adapts only the calls in `Cases/`; a golden that must change is a listed, approved deviation.

## Approved deviations

A golden that was changed, removed or added because the library output was wrong, each with its kind, the old and the new text and the reason. A removed golden has no new text and an added one has no old text. The list is `CorpusIndex.ApprovedDeviations`; the capture writes it here, so it cannot drift from the code.

- `query-read/where-reversed-operands`, Changed. Old text: `SELECT * FROM "corpus_notes" WHERE "rank" < @p0`. New text:
  `SELECT * FROM "corpus_notes" WHERE "rank" > @p0`. Reason: B1 (ORML-2): a comparison with the constant on the left (`3
  < n.Rank`) was written with the operator not mirrored, so it selected the complement; it now renders `"rank" > @p0`.
  The parameter is unchanged.
- `query-read/where-constant-left-equals`, Added. Old text: (none). New text: `SELECT * FROM "corpus_notes" WHERE "rank"
  = @p0`. Reason: ORML-51: equality with the constant on the left is symmetric and unchanged; a new case, not a change.
- `query-read/where-constant-left-not-equals`, Added. Old text: (none). New text: `SELECT * FROM "corpus_notes" WHERE
  "rank" <> @p0`. Reason: ORML-51: inequality with the constant on the left is symmetric and unchanged; a new case, not
  a change.
- `ddl-builder/insert-data-literal-kinds`, Changed. Old text: `INSERT INTO "samples" ("s", "n", "d", "b", "z", "g", "l")
  VALUES ('it''s', 5, 1.5, true, NULL, a0000000-0000-0000-0000-00000000000a, 9), ('', -1, 0.25, false, NULL,
  b0000000-0000-0000-0000-00000000000b, 0)`. New text: `INSERT INTO "samples" ("s", "n", "d", "b", "z", "g", "l") VALUES
  ('it''s', 5, 1.5, true, NULL, 'a0000000-0000-0000-0000-00000000000a', 9), ('', -1, 0.25, false, NULL,
  'b0000000-0000-0000-0000-00000000000b', 0)`. Reason: B7 (ORML-8): InsertData wrote a Guid as bare text, which is a
  syntax error; it is now a single-quoted lowercase D-format string. Nothing else on the line moves.
- `dialect/ddl-dialect`, Changed. Old text: `[10] Literal(guid) = a0000000-0000-0000-0000-00000000000a`. New text: `[10]
  Literal(guid) = 'a0000000-0000-0000-0000-00000000000a'`. Reason: B7 (ORML-8): PgDdlDialect.Literal reaches the same
  literal function as InsertData, so a Guid is quoted there too. Lines [0]-[9] are unchanged.
- `ddl-builder/insert-data-no-rows`, Removed. Old text: `INSERT INTO "samples" ("id") VALUES `. New text: (removed).
  Reason: B46 (ORML-8): an InsertData without rows rendered a trailing VALUES, which is not valid SQL; the builder now
  refuses it before recording a node, see refusals/migration-builder-insert-data-without-rows.
- `ddl-builder/delete-data-null-and-quote-values`, Removed. Old text: `DELETE FROM "samples" WHERE "name" IN ('it''s',
  NULL)`. New text: (removed). Reason: B46 (ORML-8): a DeleteData with a null value rendered a list that never matches
  NULL, so the delete silently did less than written; the builder now refuses it, see
  refusals/migration-builder-delete-data-with-a-null-value.
- `refusals/migration-builder-insert-data-without-rows`, Added. Old text: (none). New text: `System.ArgumentException:
  InsertData needs at least one row. (Parameter 'rows')`. Reason: B46 (ORML-8): an InsertData without rows is refused
  before a node is recorded.
- `refusals/migration-builder-insert-data-without-columns`, Added. Old text: (none). New text:
  `System.ArgumentException: InsertData needs at least one column. (Parameter 'columns')`. Reason: B46 (ORML-8): an
  InsertData without columns is refused before a node is recorded.
- `refusals/migration-builder-delete-data-with-a-null-value`, Added. Old text: (none). New text:
  `System.ArgumentException: DeleteData cannot match a null value: an IN list never matches NULL, so the delete would
  silently skip it. (Parameter 'values')`. Reason: B46 (ORML-8): a DeleteData with a null value is refused before a node
  is recorded.
- `ddl-builder/insert-data-temporal-enum-and-char-kinds`, Added. Old text: (none). New text: `INSERT INTO "samples"
  ("at", "shifted", "day", "status", "c", "q", "doc") VALUES ('2024-01-02T10:00:00.0000000Z',
  '2024-01-02T04:30:00.0000000Z', '2024-02-29', 'Active', 'x', '''', '{"k":"it''s"}'), ('2024-01-02T10:00:00.1234567Z',
  '2024-01-02T18:00:00.0000000Z', '2024-12-31', 'Archived', 'y', 'z', '{}')`. Reason: B7 (ORML-8): a moment, an offset
  moment, a day, an enum member, a char and a document are written as quoted text.
- `ddl-builder/delete-data-guid-enum-and-date-values`, Added. Old text: (none). New text: `DELETE FROM "samples" WHERE
  "k" IN ('a0000000-0000-0000-0000-00000000000a', 'Active', '2024-02-29')`. Reason: B7 (ORML-8): a DeleteData matches a
  Guid, an enum member and a day by their quoted text.
- `refusals/ddl-literal-non-finite-number`, Added. Old text: (none). New text: `System.ArgumentException: NaN has no
  portable literal; write a finite number. (Parameter 'value')`. Reason: B7 (ORML-8): NaN and the infinities have no
  portable literal and are refused, naming the value.
- `refusals/ddl-literal-unsupported-type`, Added. Old text: (none). New text: `System.NotSupportedException:
  System.TimeSpan has no literal form; write a string, a number, a flag, a character, a Guid, a date or time, a Jsonb or
  an enum member.`. Reason: B7 (ORML-8): a value of a type with no literal form is refused, naming the type; there is no
  bare fallback.
- `generator-diagnostics/diagnostics`, Changed. Old text: `28 diagnostic lines; no ORMLITE022 line`. New text: `29
  diagnostic lines; one added after the ORMLITE018 line: ORMLITE022 Error Declarations.cs:103: 'SoftWithTwoKeys'
  declares 2 key properties (Id, OtherId). Orm.Lite supports one key per entity: a get, update or delete by key on
  several keys would act on every row that shares the first, so none is generated for 'SoftWithTwoKeys'. Keep one [Key]
  (a property named Id is a key even without [Key]: remove the other [Key], or rename the Id property and keep its
  column with [Column]), declare no key to insert and query only, or address rows by their columns through
  Query().Where(...).`. Reason: B4 (ORML-5): an entity with more than one key property is refused with ORMLITE022; the
  ORMLITE008 line for SoftWithTwoKeys is unchanged and both diagnostics now appear for it.
- `generator-diagnostics/ormlite.entities.g.cs`, Changed. Old text: `SoftWithTwoKeysOrm declared DeleteAsync(db, Guid
  key), GetAsync(db, Guid key), GetAsync(db, conn, Guid key) and DeleteAsync(db, conn, Guid key), each built on the
  first key Id alone`. New text: `SoftWithTwoKeysOrm declares none of the four; its Descriptor, Map, Values, Column,
  Query and both InsertAsync are unchanged`. Reason: B4 (ORML-5): a get or delete by key on several keys acted on every
  row sharing the first key, so the generator withholds the keyed members of a refused entity.
- `executed/constraint-columns-* (3 goldens: from-a-constraint, falls-back-to-an-index, unknown-name; the constraint
  statement)`, Changed. Old text: `WHERE k.conname = @n AND pg_table_is_visible(k.conrelid) ORDER BY c.ord`. New text:
  `WHERE k.conname = @n AND k.contype IN ('p', 'u', 'x') AND pg_table_is_visible(k.conrelid) ORDER BY c.ord`. Reason:
  B45 (ORML-31): the constraint was found by its name and visibility alone, so a check or foreign key constraint of that
  name answered with the columns it constrains; only a primary key, unique or exclusion constraint is read now.
  Parameters and the outcome of these cases are unchanged.
- `executed/constraint-columns-* (2 goldens: falls-back-to-an-index, unknown-name; the index statement)`, Changed. Old
  text: `AND pg_table_is_visible(r.oid) AND c.attnum > 0 ORDER BY c.ord`. New text: `AND pg_table_is_visible(r.oid) AND
  c.attnum > 0 AND c.ord <= i.indnkeyatts ORDER BY c.ord`. Reason: B45 (ORML-31): indkey lists the key columns and then
  the INCLUDE columns, so a unique index with an INCLUDE column reported its included column as a colliding one; only
  the first indnkeyatts columns are read now. Parameters and the outcome of these cases are unchanged.
- `refusals/predicate-range-over-an-enum-column`, Added. Old text: (none). New text: `System.NotSupportedException:
  Cannot compare the enum column Status (CorpusStatus) with >=: enums are stored as their member NAME, so the database
  would rank the names as text, alphabetically and not by value, and the range would silently select the wrong rows.
  Nothing was executed. Compare with == or != (or Contains) against the members you mean, or read the rows and compare
  the enum values in memory.`. Reason: B10 (ORML-11): a range (<, <=, >, >=) or an ordering over an enum column compared
  the stored member NAMES as text, alphabetically, and answered with rows or an order the enum does not have; it is
  refused by name before anything is rendered.
- `refusals/predicate-range-over-an-enum-column-as-integers`, Added. Old text: (none). New text:
  `System.NotSupportedException: Cannot compare the enum column Status (CorpusStatus) with >: enums are stored as their
  member NAME, so the database would rank the names as text, alphabetically and not by value, and the range would
  silently select the wrong rows. Nothing was executed. Compare with == or != (or Contains) against the members you
  mean, or read the rows and compare the enum values in memory.`. Reason: B10 (ORML-11): a range (<, <=, >, >=) or an
  ordering over an enum column compared the stored member NAMES as text, alphabetically, and answered with rows or an
  order the enum does not have; it is refused by name before anything is rendered.
- `refusals/predicate-range-over-a-nullable-enum-column-constant-on-the-left`, Added. Old text: (none). New text:
  `System.NotSupportedException: Cannot compare the enum column Review (CorpusStatus?) with <: enums are stored as their
  member NAME, so the database would rank the names as text, alphabetically and not by value, and the range would
  silently select the wrong rows. Nothing was executed. Compare with == or != (or Contains) against the members you
  mean, or read the rows and compare the enum values in memory.`. Reason: B10 (ORML-11): a range (<, <=, >, >=) or an
  ordering over an enum column compared the stored member NAMES as text, alphabetically, and answered with rows or an
  order the enum does not have; it is refused by name before anything is rendered.
- `refusals/order-by-an-enum-column`, Added. Old text: (none). New text: `System.NotSupportedException: Cannot order by
  the enum column Status (CorpusStatus): enums are stored as their member NAME, so the database would rank the names as
  text, alphabetically and not by value, and the rows would silently come back in an order the enum does not have.
  Nothing was executed. Order by a column that holds the order you mean (an integer rank, for example), or read the rows
  and sort the enum values in memory.`. Reason: B10 (ORML-11): a range (<, <=, >, >=) or an ordering over an enum column
  compared the stored member NAMES as text, alphabetically, and answered with rows or an order the enum does not have;
  it is refused by name before anything is rendered.
- `refusals/order-by-descending-a-nullable-enum-column`, Added. Old text: (none). New text:
  `System.NotSupportedException: Cannot order by the enum column Review (CorpusStatus?): enums are stored as their
  member NAME, so the database would rank the names as text, alphabetically and not by value, and the rows would
  silently come back in an order the enum does not have. Nothing was executed. Order by a column that holds the order
  you mean (an integer rank, for example), or read the rows and sort the enum values in memory.`. Reason: B10 (ORML-11):
  a range (<, <=, >, >=) or an ordering over an enum column compared the stored member NAMES as text, alphabetically,
  and answered with rows or an order the enum does not have; it is refused by name before anything is rendered.
- `ddl-builder/create-table-all-column-shapes`, Changed. Old text: `CREATE TABLE IF NOT EXISTS "samples" ("id" uuid NOT
  NULL PRIMARY KEY, "name" text NOT NULL DEFAULT ''::text, "rank" integer DEFAULT 0, "note" text, "created_at" timestamp
  with time zone NOT NULL DEFAULT now(), "odd""name" text)`. New text: `CREATE TABLE IF NOT EXISTS "samples" ("id" uuid
  NOT NULL PRIMARY KEY, "name" text NOT NULL DEFAULT '', "rank" integer DEFAULT 0, "note" text, "created_at" timestamp
  with time zone NOT NULL DEFAULT now(), "odd""name" text)`. Reason: B62 (ORML-44): the column type and default spelled
  as text are gone from the builders; the case now builds the same table with ColumnType and Defaults, and a typed text
  default is the plain literal '' where the spelled one carried a ::text cast. Nothing else on the line moves.
- `generator-corpus-entities/diagnostics`, Changed. Old text: `4 diagnostic lines: one ORMLITE017 and three ORMLITE018
  (Migrations.cs:34, :8 and :9)`. New text: `1 diagnostic line: the ORMLITE017 line; the three ORMLITE018 lines are
  gone`. Reason: B62 (ORML-44): ORMLITE018 is retired with the members it reported, so the fixture's Sql, text AddColumn
  and text Column calls (now Raw with a provider and the typed overloads) raise no warning.
- `generator-diagnostics/diagnostics`, Changed. Old text: `29 diagnostic lines, among them 'ORMLITE018 Warning
  Declarations.cs:155: ...calls Sql(text)...'`. New text: `28 diagnostic lines: the ORMLITE018 line is gone and every
  other line is unchanged`. Reason: B62 (ORML-44): ORMLITE018 is retired with Sql(text); the fixture migration now calls
  Raw with a provider and no line of the fixture moved.
- `generator-corpus-entities/migration-runner-fresh-database`, Changed. Old text: `statement #2 is CREATE TABLE IF NOT
  EXISTS "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL
  DEFAULT now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-corpus-entities/migration-runner-partially-applied`, Changed. Old text: `statement #2 is CREATE TABLE IF
  NOT EXISTS "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL
  DEFAULT now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-demo/migration-runner-fresh-database`, Changed. Old text: `statement #2 is CREATE TABLE IF NOT EXISTS
  "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT
  now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-demo/migration-runner-partially-applied`, Changed. Old text: `statement #2 is CREATE TABLE IF NOT EXISTS
  "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT
  now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-global-namespace/migration-runner-fresh-database`, Changed. Old text: `statement #2 is CREATE TABLE IF NOT
  EXISTS "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL
  DEFAULT now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-global-namespace/migration-runner-partially-applied`, Changed. Old text: `statement #2 is CREATE TABLE IF
  NOT EXISTS "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL
  DEFAULT now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-golden-fixtures/migration-runner-fresh-database`, Changed. Old text: `statement #2 is CREATE TABLE IF NOT
  EXISTS "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL
  DEFAULT now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `generator-golden-fixtures/migration-runner-partially-applied`, Changed. Old text: `statement #2 is CREATE TABLE IF
  NOT EXISTS "schema_migrations" ("version" text NOT NULL PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL
  DEFAULT now()) and statement #3 is SELECT pg_advisory_lock(727274)`. New text: `statement #2 is SELECT
  pg_advisory_lock(727274) and statement #3 is CREATE TABLE IF NOT EXISTS "schema_migrations" ("version" text NOT NULL
  PRIMARY KEY, "applied_at" timestamp with time zone NOT NULL DEFAULT now()); no other statement, parameter or
  transaction marker moves`. Reason: B31 (ORML-23): the PostgreSQL runner created the history table before it took the
  advisory lock, so replicas migrating an empty database could collide on the catalog; the lock now comes first, and the
  two statements trade places.
- `query-read/order-by-ascending`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC`. New text:
  `SELECT * FROM "corpus_notes" ORDER BY "title" ASC NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy,
  OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL put NULLs
  last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order term is
  now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `query-read/order-by-descending`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "rank" DESC`. New text:
  `SELECT * FROM "corpus_notes" ORDER BY "rank" DESC NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy,
  OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL put NULLs
  last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order term is
  now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `query-read/order-then-by-chain`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC, "rank" DESC,
  "id" ASC`. New text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC NULLS LAST, "rank" DESC NULLS LAST, "id" ASC
  NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate
  orders stated no NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and
  OrderBy(...).Take(n) read different rows; every plain order term is now NULLS LAST, in both directions and on both
  providers. Nothing else on the line moves.
- `query-read/order-by-replaces-earlier-order`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "views" DESC`.
  New text: `SELECT * FROM "corpus_notes" ORDER BY "views" DESC NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy,
  OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL put NULLs
  last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order term is
  now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `query-read/order-by-predicate`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY ("rank" > @p0) ASC`. New
  text: `SELECT * FROM "corpus_notes" ORDER BY ("rank" > @p0) ASC NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy,
  OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL put NULLs
  last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order term is
  now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `query-read/order-then-by-predicate`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC, ("active"
  = true) ASC`. New text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC NULLS LAST, ("active" = true) ASC NULLS
  LAST`. Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders
  stated no NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and OrderBy(...).Take(n)
  read different rows; every plain order term is now NULLS LAST, in both directions and on both providers. Nothing else
  on the line moves.
- `query-read/order-by-predicate-with-parameter-before-where-parameter`, Changed. Old text: `SELECT * FROM
  "corpus_notes" WHERE "title" = @p1 ORDER BY ("title" = @p0) ASC`. New text: `SELECT * FROM "corpus_notes" WHERE
  "title" = @p1 ORDER BY ("title" = @p0) ASC NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending,
  ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL put NULLs last in an
  ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order term is now NULLS
  LAST, in both directions and on both providers. Nothing else on the line moves.
- `query-read/limit-full-statement-soft`, Changed. Old text: `SELECT * FROM "corpus_tasks" WHERE "deleted_at" IS NULL
  AND "rank" > @p0 ORDER BY "rank" DESC, "name" ASC LIMIT 5 OFFSET 10`. New text: `SELECT * FROM "corpus_tasks" WHERE
  "deleted_at" IS NULL AND "rank" > @p0 ORDER BY "rank" DESC NULLS LAST, "name" ASC NULLS LAST LIMIT 5 OFFSET 10`.
  Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no
  NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and OrderBy(...).Take(n) read
  different rows; every plain order term is now NULLS LAST, in both directions and on both providers. Nothing else on
  the line moves.
- `query-read/project-columns-soft-where-order`, Changed. Old text: `SELECT "id", "rank" FROM "corpus_tasks" WHERE
  "deleted_at" IS NULL AND "rank" > @p0 ORDER BY "name" ASC LIMIT 3`. New text: `SELECT "id", "rank" FROM "corpus_tasks"
  WHERE "deleted_at" IS NULL AND "rank" > @p0 ORDER BY "name" ASC NULLS LAST LIMIT 3`. Reason: B11 (ORML-12): a plain
  OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL
  put NULLs last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order
  term is now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `locks-intervals/lock-for-update-with-order-limit-offset`, Changed. Old text: `SELECT * FROM "corpus_notes" WHERE
  "rank" > @p0 ORDER BY "title" ASC LIMIT 2 OFFSET 1 FOR UPDATE`. New text: `SELECT * FROM "corpus_notes" WHERE "rank" >
  @p0 ORDER BY "title" ASC NULLS LAST LIMIT 2 OFFSET 1 FOR UPDATE`. Reason: B11 (ORML-12): a plain OrderBy,
  OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL put NULLs
  last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order term is
  now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `locks-intervals/lock-for-no-key-update-with-order-limit`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY
  "rank" DESC LIMIT 1 FOR NO KEY UPDATE`. New text: `SELECT * FROM "corpus_notes" ORDER BY "rank" DESC NULLS LAST LIMIT
  1 FOR NO KEY UPDATE`. Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the
  predicate orders stated no NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and
  OrderBy(...).Take(n) read different rows; every plain order term is now NULLS LAST, in both directions and on both
  providers. Nothing else on the line moves.
- `json/order-json-key-then-by`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC, ("payload" ->>
  @p0) DESC NULLS LAST`. New text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC NULLS LAST, ("payload" ->> @p0)
  DESC NULLS LAST`. Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the
  predicate orders stated no NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and
  OrderBy(...).Take(n) read different rows; every plain order term is now NULLS LAST, in both directions and on both
  providers. Nothing else on the line moves.
- `json/order-json-key-of-then-by`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC, (SELECT (CASE
  WHEN pg_input_is_valid((s0."doc" ->> @p0), 'numeric') THEN (s0."doc" ->> @p0) END)::numeric FROM "corpus_tags" s0
  WHERE s0."note_id" = "corpus_notes"."id") DESC NULLS LAST`. New text: `SELECT * FROM "corpus_notes" ORDER BY "title"
  ASC NULLS LAST, (SELECT (CASE WHEN pg_input_is_valid((s0."doc" ->> @p0), 'numeric') THEN (s0."doc" ->> @p0)
  END)::numeric FROM "corpus_tags" s0 WHERE s0."note_id" = "corpus_notes"."id") DESC NULLS LAST`. Reason: B11 (ORML-12):
  a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so
  PostgreSQL put NULLs last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every
  plain order term is now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `executed/query-constructor-maps-the-rows-it-reads`, Changed. Old text: `SELECT * FROM "corpus_notes" ORDER BY "title"
  ASC`. New text: `SELECT * FROM "corpus_notes" ORDER BY "title" ASC NULLS LAST`. Reason: B11 (ORML-12): a plain
  OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no NULL placement, so PostgreSQL
  put NULLs last in an ascending order and SQLite first and OrderBy(...).Take(n) read different rows; every plain order
  term is now NULLS LAST, in both directions and on both providers. Nothing else on the line moves.
- `executed/query-to-list-empty`, Changed. Old text: `SELECT * FROM "corpus_notes" WHERE "rank" > @p0 ORDER BY "title"
  ASC LIMIT 5`. New text: `SELECT * FROM "corpus_notes" WHERE "rank" > @p0 ORDER BY "title" ASC NULLS LAST LIMIT 5`.
  Reason: B11 (ORML-12): a plain OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders stated no
  NULL placement, so PostgreSQL put NULLs last in an ascending order and SQLite first and OrderBy(...).Take(n) read
  different rows; every plain order term is now NULLS LAST, in both directions and on both providers. Nothing else on
  the line moves.
- `query-upsert/do-update-increment`, Changed. Old text: `INSERT INTO "corpus_notes" ("id", "title", "rank") VALUES
  (@p0, @p1, @p2) ON CONFLICT ("id") DO UPDATE SET "views" = "corpus_notes"."views" + 3`. New text: `INSERT INTO
  "corpus_notes" ("id", "title", "rank") VALUES (@p0, @p1, @p2) ON CONFLICT ("id") DO UPDATE SET "views" =
  COALESCE("corpus_notes"."views", 0) + 3`. Reason: B15 (ORML-14): an upsert Increment added to the stored column as it
  stood, so a counter that was NULL stayed NULL however often the conflict hit, unlike SetIncrement; it now counts from
  zero, COALESCE(column, 0) + n. Nothing else on the line moves.
- `query-upsert/do-update-mixed-sets`, Changed. Old text: `INSERT INTO "corpus_notes" ("id", "title", "rank") VALUES
  (@p0, @p1, @p2) ON CONFLICT ("id") DO UPDATE SET "title" = @p3, "views" = "corpus_notes"."views" + 1, "created_at" =
  now(), "rank" = @p4`. New text: `INSERT INTO "corpus_notes" ("id", "title", "rank") VALUES (@p0, @p1, @p2) ON CONFLICT
  ("id") DO UPDATE SET "title" = @p3, "views" = COALESCE("corpus_notes"."views", 0) + 1, "created_at" = now(), "rank" =
  @p4`. Reason: B15 (ORML-14): an upsert Increment added to the stored column as it stood, so a counter that was NULL
  stayed NULL however often the conflict hit, unlike SetIncrement; it now counts from zero, COALESCE(column, 0) + n.
  Nothing else on the line moves.
- `executed/query-upsert-do-update-returning`, Changed. Old text: `INSERT INTO "corpus_notes" ("id", "title") VALUES
  (@p0, @p1) ON CONFLICT ("id") DO UPDATE SET "title" = @p2, "views" = "corpus_notes"."views" + 1 RETURNING "id",
  "title"`. New text: `INSERT INTO "corpus_notes" ("id", "title") VALUES (@p0, @p1) ON CONFLICT ("id") DO UPDATE SET
  "title" = @p2, "views" = COALESCE("corpus_notes"."views", 0) + 1 RETURNING "id", "title"`. Reason: B15 (ORML-14): an
  upsert Increment added to the stored column as it stood, so a counter that was NULL stayed NULL however often the
  conflict hit, unlike SetIncrement; it now counts from zero, COALESCE(column, 0) + n. Nothing else on the line moves.
- `query-upsert/do-update-without-sets-becomes-do-nothing`, Removed. Old text: `INSERT INTO "corpus_notes" ("id",
  "title", "rank") VALUES (@p0, @p1, @p2) ON CONFLICT ("id") DO NOTHING`. New text: (removed). Reason: B16 (ORML-14): an
  OnConflict.DoUpdate that names no UpsertSet was written as DO NOTHING without a word (on a soft-delete entity also
  without the live-row guard); it is now refused by name, so the golden that pinned the old text is removed and the
  refusal is added.
- `query-upsert/soft-do-update-without-sets-has-no-guard`, Removed. Old text: `INSERT INTO "corpus_tasks" ("id",
  "tenant_id", "name", "rank") VALUES (@p0, @p1, @p2, @p3) ON CONFLICT ("id") DO NOTHING`. New text: (removed). Reason:
  B16 (ORML-14): an OnConflict.DoUpdate that names no UpsertSet was written as DO NOTHING without a word (on a
  soft-delete entity also without the live-row guard); it is now refused by name, so the golden that pinned the old text
  is removed and the refusal is added.
- `refusals/upsert-update-without-sets`, Added. Old text: (none). New text: `System.InvalidOperationException:
  OnConflict.DoUpdate names no UpsertSet, so it would update nothing and be written as a conflict that is ignored,
  silently, and a soft-deleted row in the way would be neither updated nor reported. Nothing was executed. Pass the sets
  to apply (UpsertSet.Inserted, Increment or Now), or use OnConflict.DoNothing.`. Reason: B16 (ORML-14): an
  OnConflict.DoUpdate that names no UpsertSet was written as DO NOTHING without a word (on a soft-delete entity also
  without the live-row guard); it is now refused by name, so the golden that pinned the old text is removed and the
  refusal is added.
- `refusals/soft-upsert-update-without-sets`, Added. Old text: (none). New text: `System.InvalidOperationException:
  OnConflict.DoUpdate names no UpsertSet, so it would update nothing and be written as a conflict that is ignored,
  silently, and a soft-deleted row in the way would be neither updated nor reported. Nothing was executed. Pass the sets
  to apply (UpsertSet.Inserted, Increment or Now), or use OnConflict.DoNothing.`. Reason: B16 (ORML-14): an
  OnConflict.DoUpdate that names no UpsertSet was written as DO NOTHING without a word (on a soft-delete entity also
  without the live-row guard); it is now refused by name, so the golden that pinned the old text is removed and the
  refusal is added.
- `generator-diagnostics/diagnostics`, Changed. Old text: `the two ORMLITE001 lines (UnmappedType.Bag,
  UnmappedType.Span) say: 'Add a mapping to OrmGenerator.Classify, or type the property as Neemle.Orm.Lite.Jsonb to
  store it as a jsonb column.'`. New text: `the two ORMLITE001 lines say: 'Use a supported type (string, int, long,
  bool, double, decimal, Guid, DateTime, DateOnly or an enum, each also nullable), type the property as
  Neemle.Orm.Lite.Jsonb to store it as a jsonb column, or mark it [NotAColumn] to leave it out.'; every other line is
  unchanged`. Reason: B29 (ORML-22): the old text pointed at a method of the generator source, which a user of the
  package cannot change; the message now names what the user can do. The property, location and the rest of the text are
  unchanged.
- `ddl-builder/insert-data-backslash-and-quote-text`, Added. Old text: (none). New text: `INSERT INTO "samples" ("a",
  "b", "c") VALUES (E'a\\b', E'x\\''); DROP TABLE t; --', E'\\'), ('plain', E'\\', E'{"k":"a\\"b"}')`. Reason: B7
  follow-up (ORML-47): with standard_conforming_strings off a backslash in a quote-doubled string escapes the next
  character and can end the literal early; PostgreSQL now writes a string that holds a backslash as an escape string
  E'...' with the backslash and the quote doubled, which means the same text under either setting. A string without a
  backslash, and every SQLite string, is unchanged.
- `ddl-builder/delete-data-backslash-text`, Added. Old text: (none). New text: `DELETE FROM "samples" WHERE "k" IN
  (E'a\\b', 'plain', E'end\\')`. Reason: B7 follow-up (ORML-47): with standard_conforming_strings off a backslash in a
  quote-doubled string escapes the next character and can end the literal early; PostgreSQL now writes a string that
  holds a backslash as an escape string E'...' with the backslash and the quote doubled, which means the same text under
  either setting. A string without a backslash, and every SQLite string, is unchanged.
- `refusals/ddl-literal-string-with-nul`, Added. Old text: (none). New text: `System.ArgumentException: a string or
  character value holds a NUL character (U+0000), which no SQL text literal can carry; remove it from the value.
  (Parameter 'value')`. Reason: B7 follow-up (ORML-47): a string, a char or a document holding U+0000 has no SQL text
  literal (PostgreSQL rejects it, SQLite cuts the statement at it); it is refused by name before a node is recorded. The
  message never carries the value.
- `refusals/ddl-literal-string-with-a-lone-surrogate`, Added. Old text: (none). New text: `System.ArgumentException: a
  string or character value holds half of a surrogate pair (a lone surrogate), which is not valid text and has no SQL
  text literal; remove it from the value. (Parameter 'value')`. Reason: B7 follow-up (ORML-47): a string or char holding
  half of a surrogate pair is not valid text and no driver writes it faithfully; it is refused by name before a node is
  recorded. A well-formed pair is unchanged.
- `generator-demo/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift lookup and name refusal, one per index)`, Changed. Old text:
  `current_index := to_regclass('"ix_notes_tenant_id_title_live"');`. New text: `SELECT ic.oid INTO current_index FROM
  pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"notes"') AND ic.relname =
  'ix_notes_tenant_id_title_live'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_notes_tenant_id_title_live' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"notes"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of table
  %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without its
  index. Rename the index.', 'ix_notes_tenant_id_title_live', 'notes'; END IF;`. Reason: B44 (ORML-30): the drift block
  found an index by its bare name through search_path, so two tables that declared one index name skipped each other's
  CREATE and dropped each other's index; it now reads the index through pg_index.indrelid of its own table and refuses a
  name another relation of the schema holds. Every drift block of the set changes this way; the fragments are listed per
  index.
- `generator-demo/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift compare, one per table)`, Changed. Old text: `IF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>`. New text: `IF
  replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.',
  quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid =
  to_regclass('"notes"')), ' USING '), ' USING ') <>`. Reason: B48 (ORML-30): the current definition was stripped of its
  table by ' ON \S+ USING ', which cannot span whitespace, so a table name with whitespace always differed and its index
  was rebuilt on every run; the exact schema-qualified table text is removed instead. The probe side keeps the pattern:
  its table name is the fixed probe name. The fragment is listed once per table of the set.
- `generator-corpus-entities/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift lookup and name refusal, one per index)`, Changed. Old text:
  `current_index := to_regclass('"ix_ce_accounts_code_live"');\ncurrent_index :=
  to_regclass('"ix_ce_accounts_name"');\ncurrent_index := to_regclass('"ix_ce_everything_searchable"');\ncurrent_index
  := to_regclass('"ix_ce_everything_unique"');\ncurrent_index :=
  to_regclass('"ix_ce_projects_tenant_id_title_live"');\ncurrent_index :=
  to_regclass('"ix_ce_projects_title"');\ncurrent_index := to_regclass('"ix_ce_tasks_project_id_name"');\ncurrent_index
  := to_regclass('"ix_projects_state"');`. New text: `SELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic
  ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_accounts"') AND ic.relname = 'ix_ce_accounts_code_live';
  IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_ce_accounts_code_live' AND
  x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"ce_accounts"'))) THEN RAISE
  EXCEPTION 'Index name % is already held by another relation in the schema of table %: PostgreSQL index names are
  schema-global, so creating it would be skipped and the table would stay without its index. Rename the index.',
  'ix_ce_accounts_code_live', 'ce_accounts'; END IF;\nSELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic
  ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_accounts"') AND ic.relname = 'ix_ce_accounts_name'; IF
  current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_ce_accounts_name' AND x.relnamespace
  = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"ce_accounts"'))) THEN RAISE EXCEPTION 'Index name
  % is already held by another relation in the schema of table %: PostgreSQL index names are schema-global, so creating
  it would be skipped and the table would stay without its index. Rename the index.', 'ix_ce_accounts_name',
  'ce_accounts'; END IF;\nSELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
  WHERE i.indrelid = to_regclass('"ce_everything"') AND ic.relname = 'ix_ce_everything_searchable'; IF current_index IS
  NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_ce_everything_searchable' AND x.relnamespace = (SELECT
  t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"ce_everything"'))) THEN RAISE EXCEPTION 'Index name % is
  already held by another relation in the schema of table %: PostgreSQL index names are schema-global, so creating it
  would be skipped and the table would stay without its index. Rename the index.', 'ix_ce_everything_searchable',
  'ce_everything'; END IF;\nSELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
  WHERE i.indrelid = to_regclass('"ce_everything"') AND ic.relname = 'ix_ce_everything_unique'; IF current_index IS NULL
  AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_ce_everything_unique' AND x.relnamespace = (SELECT
  t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"ce_everything"'))) THEN RAISE EXCEPTION 'Index name % is
  already held by another relation in the schema of table %: PostgreSQL index names are schema-global, so creating it
  would be skipped and the table would stay without its index. Rename the index.', 'ix_ce_everything_unique',
  'ce_everything'; END IF;\nSELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
  WHERE i.indrelid = to_regclass('"ce_projects"') AND ic.relname = 'ix_ce_projects_tenant_id_title_live'; IF
  current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_ce_projects_tenant_id_title_live' AND
  x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"ce_projects"'))) THEN RAISE
  EXCEPTION 'Index name % is already held by another relation in the schema of table %: PostgreSQL index names are
  schema-global, so creating it would be skipped and the table would stay without its index. Rename the index.',
  'ix_ce_projects_tenant_id_title_live', 'ce_projects'; END IF;\nSELECT ic.oid INTO current_index FROM pg_index i JOIN
  pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_projects"') AND ic.relname =
  'ix_ce_projects_title'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_ce_projects_title' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"ce_projects"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of
  table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_ce_projects_title', 'ce_projects'; END IF;\nSELECT ic.oid INTO current_index FROM
  pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_tasks"') AND ic.relname =
  'ix_ce_tasks_project_id_name'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_ce_tasks_project_id_name' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"ce_tasks"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of
  table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_ce_tasks_project_id_name', 'ce_tasks'; END IF;\nSELECT ic.oid INTO current_index
  FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_projects"') AND
  ic.relname = 'ix_projects_state'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_projects_state' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"ce_projects"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of
  table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_projects_state', 'ce_projects'; END IF;`. Reason: B44 (ORML-30): the drift block
  found an index by its bare name through search_path, so two tables that declared one index name skipped each other's
  CREATE and dropped each other's index; it now reads the index through pg_index.indrelid of its own table and refuses a
  name another relation of the schema holds. Every drift block of the set changes this way; the fragments are listed per
  index.
- `generator-corpus-entities/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift compare, one per table)`, Changed. Old text: `IF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>\nIF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>\nIF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>\nIF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>`. New text: `IF
  replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.',
  quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid =
  to_regclass('"ce_accounts"')), ' USING '), ' USING ') <>\nIF replace(pg_get_indexdef(current_index), concat(' ON ',
  (SELECT concat(quote_ident(n.nspname), '.', quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid =
  c.relnamespace WHERE c.oid = to_regclass('"ce_everything"')), ' USING '), ' USING ') <>\nIF
  replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.',
  quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid =
  to_regclass('"ce_projects"')), ' USING '), ' USING ') <>\nIF replace(pg_get_indexdef(current_index), concat(' ON ',
  (SELECT concat(quote_ident(n.nspname), '.', quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid =
  c.relnamespace WHERE c.oid = to_regclass('"ce_tasks"')), ' USING '), ' USING ') <>`. Reason: B48 (ORML-30): the
  current definition was stripped of its table by ' ON \S+ USING ', which cannot span whitespace, so a table name with
  whitespace always differed and its index was rebuilt on every run; the exact schema-qualified table text is removed
  instead. The probe side keeps the pattern: its table name is the fixed probe name. The fragment is listed once per
  table of the set.
- `generator-golden-fixtures/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift lookup and name refusal, one per index)`, Changed. Old text:
  `current_index := to_regclass('"ix_fixture_notes_title"');`. New text: `SELECT ic.oid INTO current_index FROM pg_index
  i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"fixture_notes"') AND ic.relname =
  'ix_fixture_notes_title'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_fixture_notes_title' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"fixture_notes"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema
  of table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_fixture_notes_title', 'fixture_notes'; END IF;`. Reason: B44 (ORML-30): the drift
  block found an index by its bare name through search_path, so two tables that declared one index name skipped each
  other's CREATE and dropped each other's index; it now reads the index through pg_index.indrelid of its own table and
  refuses a name another relation of the schema holds. Every drift block of the set changes this way; the fragments are
  listed per index.
- `generator-golden-fixtures/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift compare, one per table)`, Changed. Old text: `IF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>`. New text: `IF
  replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.',
  quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid =
  to_regclass('"fixture_notes"')), ' USING '), ' USING ') <>`. Reason: B48 (ORML-30): the current definition was
  stripped of its table by ' ON \S+ USING ', which cannot span whitespace, so a table name with whitespace always
  differed and its index was rebuilt on every run; the exact schema-qualified table text is removed instead. The probe
  side keeps the pattern: its table name is the fixed probe name. The fragment is listed once per table of the set.
- `generator-global-namespace/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift lookup and name refusal, one per index)`, Changed. Old text:
  `current_index := to_regclass('"ix_gn_orchards_name_live"');\ncurrent_index :=
  to_regclass('"ix_gn_trees_orchard_id_species"');`. New text: `SELECT ic.oid INTO current_index FROM pg_index i JOIN
  pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"gn_orchards"') AND ic.relname =
  'ix_gn_orchards_name_live'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_gn_orchards_name_live' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"gn_orchards"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of
  table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_gn_orchards_name_live', 'gn_orchards'; END IF;\nSELECT ic.oid INTO current_index
  FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"gn_trees"') AND ic.relname
  = 'ix_gn_trees_orchard_id_species'; IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_gn_trees_orchard_id_species' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"gn_trees"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of
  table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_gn_trees_orchard_id_species', 'gn_trees'; END IF;`. Reason: B44 (ORML-30): the
  drift block found an index by its bare name through search_path, so two tables that declared one index name skipped
  each other's CREATE and dropped each other's index; it now reads the index through pg_index.indrelid of its own table
  and refuses a name another relation of the schema holds. Every drift block of the set changes this way; the fragments
  are listed per index.
- `generator-global-namespace/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (drift compare, one per table)`, Changed. Old text: `IF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>\nIF
  regexp_replace(pg_get_indexdef(current_index), ' ON \S+ USING ', ' USING ') <>`. New text: `IF
  replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.',
  quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid =
  to_regclass('"gn_orchards"')), ' USING '), ' USING ') <>\nIF replace(pg_get_indexdef(current_index), concat(' ON ',
  (SELECT concat(quote_ident(n.nspname), '.', quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid =
  c.relnamespace WHERE c.oid = to_regclass('"gn_trees"')), ' USING '), ' USING ') <>`. Reason: B48 (ORML-30): the
  current definition was stripped of its table by ' ON \S+ USING ', which cannot span whitespace, so a table name with
  whitespace always differed and its index was rebuilt on every run; the exact schema-qualified table text is removed
  instead. The probe side keeps the pattern: its table name is the fixed probe name. The fragment is listed once per
  table of the set.
- `ddl-helpers/live-only-up-plain-key`, Changed. Old text: `DO $lc$ DECLARE owner_constraint name; BEGIN PERFORM
  set_config('lock_timeout', '5s', true); IF to_regclass('"uq_samples_name"') IS NOT NULL THEN CREATE UNIQUE INDEX IF
  NOT EXISTS "ix_samples_name_live" ON "samples" ("name") WHERE "deleted_at" IS NULL; COMMENT ON INDEX
  "ix_samples_name_live" IS '0200_live_only'; SELECT c.conname INTO owner_constraint FROM pg_constraint c WHERE
  c.conrelid = to_regclass('"samples"') AND c.conindid = to_regclass('"uq_samples_name"') AND c.contype IN ('u', 'p');
  IF owner_constraint IS NULL THEN DROP INDEX "uq_samples_name"; ELSE EXECUTE format('ALTER TABLE "samples" DROP
  CONSTRAINT %I', owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS "ix_samples_name_live" ON "samples"
  ("name") WHERE "deleted_at" IS NULL; END IF; END $lc$`. New text: `DO $lc$ DECLARE owner_constraint name; total_index
  oid; live_index oid; BEGIN PERFORM set_config('lock_timeout', '5s', true); SELECT ic.oid INTO total_index FROM
  pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"samples"') AND ic.relname =
  'uq_samples_name'; SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE
  i.indrelid = to_regclass('"samples"') AND ic.relname = 'ix_samples_name_live'; IF live_index IS NULL AND EXISTS
  (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_samples_name_live' AND x.relnamespace = (SELECT t.relnamespace FROM
  pg_class t WHERE t.oid = to_regclass('"samples"'))) THEN RAISE EXCEPTION 'Index name % is already held by another
  relation in the schema of table %: PostgreSQL index names are schema-global, so creating it would be skipped and the
  table would stay without its index. Rename the index.', 'ix_samples_name_live', 'samples'; END IF; IF total_index IS
  NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "ix_samples_name_live" ON "samples" ("name") WHERE "deleted_at" IS
  NULL; COMMENT ON INDEX "ix_samples_name_live" IS '0200_live_only'; SELECT c.conname INTO owner_constraint FROM
  pg_constraint c WHERE c.conrelid = to_regclass('"samples"') AND c.conindid = total_index AND c.contype IN ('u', 'p');
  IF owner_constraint IS NULL THEN EXECUTE 'DROP INDEX ' \|\| total_index::regclass; ELSE EXECUTE format('ALTER TABLE
  "samples" DROP CONSTRAINT %I', owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS
  "ix_samples_name_live" ON "samples" ("name") WHERE "deleted_at" IS NULL; END IF; END $lc$`. Reason: B44 (ORML-30): the
  Up block found the total index by its bare name through search_path, so a total index of another table made it drop
  that table's index, and a live name held by another table made CREATE ... IF NOT EXISTS skip while the total index was
  dropped; both indexes are now read through pg_index.indrelid of the table, the total index is dropped by oid, and a
  live name held by another relation is refused.
- `ddl-helpers/live-only-up-lowered-composite-key-undeclared-live-index`, Changed. Old text: `DO $lc$ DECLARE
  owner_constraint name; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF to_regclass('"uq_samples_name"') IS
  NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "ix_samples_name_live" ON "samples" (lower("name"), "scope") WHERE
  "deleted_at" IS NULL; COMMENT ON INDEX "ix_samples_name_live" IS '0200_live_only'; SELECT c.conname INTO
  owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('"samples"') AND c.conindid =
  to_regclass('"uq_samples_name"') AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN DROP INDEX
  "uq_samples_name"; ELSE EXECUTE format('ALTER TABLE "samples" DROP CONSTRAINT %I', owner_constraint); END IF; ELSE
  CREATE UNIQUE INDEX IF NOT EXISTS "ix_samples_name_live" ON "samples" (lower("name"), "scope") WHERE "deleted_at" IS
  NULL; END IF; END $lc$`. New text: `DO $lc$ DECLARE owner_constraint name; total_index oid; live_index oid; BEGIN
  PERFORM set_config('lock_timeout', '5s', true); SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class ic ON
  ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"samples"') AND ic.relname = 'uq_samples_name'; SELECT ic.oid
  INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"samples"')
  AND ic.relname = 'ix_samples_name_live'; IF live_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'ix_samples_name_live' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid =
  to_regclass('"samples"'))) THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of
  table %: PostgreSQL index names are schema-global, so creating it would be skipped and the table would stay without
  its index. Rename the index.', 'ix_samples_name_live', 'samples'; END IF; IF total_index IS NOT NULL THEN CREATE
  UNIQUE INDEX IF NOT EXISTS "ix_samples_name_live" ON "samples" (lower("name"), "scope") WHERE "deleted_at" IS NULL;
  COMMENT ON INDEX "ix_samples_name_live" IS '0200_live_only'; SELECT c.conname INTO owner_constraint FROM pg_constraint
  c WHERE c.conrelid = to_regclass('"samples"') AND c.conindid = total_index AND c.contype IN ('u', 'p'); IF
  owner_constraint IS NULL THEN EXECUTE 'DROP INDEX ' \|\| total_index::regclass; ELSE EXECUTE format('ALTER TABLE
  "samples" DROP CONSTRAINT %I', owner_constraint); END IF; ELSE CREATE UNIQUE INDEX IF NOT EXISTS
  "ix_samples_name_live" ON "samples" (lower("name"), "scope") WHERE "deleted_at" IS NULL; END IF; END $lc$`. Reason:
  B44 (ORML-30): the Up block found the total index by its bare name through search_path, so a total index of another
  table made it drop that table's index, and a live name held by another table made CREATE ... IF NOT EXISTS skip while
  the total index was dropped; both indexes are now read through pg_index.indrelid of the table, the total index is
  dropped by oid, and a live name held by another relation is refused.
- `ddl-helpers/live-only-down-plain-key-declared-live-index`, Changed. Old text: `DO $lc$ DECLARE duplicate_key text;
  BEGIN PERFORM set_config('lock_timeout', '5s', true); IF
  coalesce(obj_description(to_regclass('"ix_samples_name_live"'), 'pg_class'), '') = '0200_live_only' THEN SELECT
  concat_ws(', ', "name"::text) INTO duplicate_key FROM "samples" WHERE "name" IS NOT NULL GROUP BY "name" HAVING
  count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot recreate unique index uq_samples_name
  on samples (name): duplicate key % exists, nothing was deleted', quote_literal(duplicate_key); END IF; CREATE UNIQUE
  INDEX IF NOT EXISTS "uq_samples_name" ON "samples" ("name"); COMMENT ON INDEX "ix_samples_name_live" IS NULL; END IF;
  END $lc$`. New text: `DO $lc$ DECLARE duplicate_key text; total_index oid; live_index oid; BEGIN PERFORM
  set_config('lock_timeout', '5s', true); SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid =
  i.indexrelid WHERE i.indrelid = to_regclass('"samples"') AND ic.relname = 'ix_samples_name_live'; SELECT ic.oid INTO
  total_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"samples"') AND
  ic.relname = 'uq_samples_name'; IF coalesce(obj_description(live_index, 'pg_class'), '') = '0200_live_only' THEN
  SELECT concat_ws(', ', "name"::text) INTO duplicate_key FROM "samples" WHERE "name" IS NOT NULL GROUP BY "name" HAVING
  count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot recreate unique index uq_samples_name
  on samples (name): duplicate key % exists, nothing was deleted', quote_literal(duplicate_key); END IF; IF total_index
  IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'uq_samples_name' AND x.relnamespace = (SELECT
  t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"samples"'))) THEN RAISE EXCEPTION 'Index name % is already
  held by another relation in the schema of table %: PostgreSQL index names are schema-global, so creating it would be
  skipped and the table would stay without its index. Rename the index.', 'uq_samples_name', 'samples'; END IF; CREATE
  UNIQUE INDEX IF NOT EXISTS "uq_samples_name" ON "samples" ("name"); EXECUTE format('COMMENT ON INDEX %s IS NULL',
  live_index::regclass); END IF; END $lc$`. Reason: B44 (ORML-30): the Down block read the live marker and dropped or
  released the live index by its bare name, and recreated the total index with IF NOT EXISTS under a name another table
  may hold; both indexes are now read through pg_index.indrelid of the table, the live index is released by oid, and a
  total name held by another relation is refused.
- `ddl-helpers/live-only-down-lowered-composite-key-undeclared-live-index`, Changed. Old text: `DO $lc$ DECLARE
  duplicate_key text; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF
  coalesce(obj_description(to_regclass('"ix_samples_name_live"'), 'pg_class'), '') = '0200_live_only' THEN SELECT
  concat_ws(', ', lower("name")::text, "scope"::text) INTO duplicate_key FROM "samples" WHERE "name" IS NOT NULL AND
  "scope" IS NOT NULL GROUP BY lower("name"), "scope" HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN
  RAISE EXCEPTION 'cannot recreate unique index uq_samples_name on samples (lower(name), scope): duplicate key % exists,
  nothing was deleted', quote_literal(duplicate_key); END IF; CREATE UNIQUE INDEX IF NOT EXISTS "uq_samples_name" ON
  "samples" (lower("name"), "scope"); DROP INDEX "ix_samples_name_live"; END IF; END $lc$`. New text: `DO $lc$ DECLARE
  duplicate_key text; total_index oid; live_index oid; BEGIN PERFORM set_config('lock_timeout', '5s', true); SELECT
  ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid =
  to_regclass('"samples"') AND ic.relname = 'ix_samples_name_live'; SELECT ic.oid INTO total_index FROM pg_index i JOIN
  pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"samples"') AND ic.relname = 'uq_samples_name';
  IF coalesce(obj_description(live_index, 'pg_class'), '') = '0200_live_only' THEN SELECT concat_ws(', ',
  lower("name")::text, "scope"::text) INTO duplicate_key FROM "samples" WHERE "name" IS NOT NULL AND "scope" IS NOT NULL
  GROUP BY lower("name"), "scope" HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL THEN RAISE EXCEPTION 'cannot
  recreate unique index uq_samples_name on samples (lower(name), scope): duplicate key % exists, nothing was deleted',
  quote_literal(duplicate_key); END IF; IF total_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname =
  'uq_samples_name' AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('"samples"')))
  THEN RAISE EXCEPTION 'Index name % is already held by another relation in the schema of table %: PostgreSQL index
  names are schema-global, so creating it would be skipped and the table would stay without its index. Rename the
  index.', 'uq_samples_name', 'samples'; END IF; CREATE UNIQUE INDEX IF NOT EXISTS "uq_samples_name" ON "samples"
  (lower("name"), "scope"); EXECUTE 'DROP INDEX ' \|\| live_index::regclass; END IF; END $lc$`. Reason: B44 (ORML-30):
  the Down block read the live marker and dropped or released the live index by its bare name, and recreated the total
  index with IF NOT EXISTS under a name another table may hold; both indexes are now read through pg_index.indrelid of
  the table, the live index is released by oid, and a total name held by another relation is refused.
- `generator-corpus-entities/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (live-only Up block)`, Changed. Old text: `DO $lc$ DECLARE owner_constraint
  name; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF to_regclass('"uq_projects_title"') IS NOT NULL THEN
  CREATE UNIQUE INDEX IF NOT EXISTS "ix_projects_title_live" ON "ce_projects" ("tenant_id", lower("title")) WHERE
  "deleted_at" IS NULL; COMMENT ON INDEX "ix_projects_title_live" IS '0400_helper_blocks'; SELECT c.conname INTO
  owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('"ce_projects"') AND c.conindid =
  to_regclass('"uq_projects_title"') AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN DROP INDEX
  "uq_projects_title"; ELSE EXECUTE format('ALTER TABLE "ce_projects" DROP CONSTRAINT %I', owner_constraint); END IF;
  ELSE CREATE UNIQUE INDEX IF NOT EXISTS "ix_projects_title_live" ON "ce_projects" ("tenant_id", lower("title")) WHERE
  "deleted_at" IS NULL; END IF; END $lc$`. New text: `DO $lc$ DECLARE owner_constraint name; total_index oid; live_index
  oid; BEGIN PERFORM set_config('lock_timeout', '5s', true); SELECT ic.oid INTO total_index FROM pg_index i JOIN
  pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_projects"') AND ic.relname =
  'uq_projects_title'; SELECT ic.oid INTO live_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid WHERE
  i.indrelid = to_regclass('"ce_projects"') AND ic.relname = 'ix_projects_title_live'; IF live_index IS NULL AND EXISTS
  (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_projects_title_live' AND x.relnamespace = (SELECT t.relnamespace FROM
  pg_class t WHERE t.oid = to_regclass('"ce_projects"'))) THEN RAISE EXCEPTION 'Index name % is already held by another
  relation in the schema of table %: PostgreSQL index names are schema-global, so creating it would be skipped and the
  table would stay without its index. Rename the index.', 'ix_projects_title_live', 'ce_projects'; END IF; IF
  total_index IS NOT NULL THEN CREATE UNIQUE INDEX IF NOT EXISTS "ix_projects_title_live" ON "ce_projects" ("tenant_id",
  lower("title")) WHERE "deleted_at" IS NULL; COMMENT ON INDEX "ix_projects_title_live" IS '0400_helper_blocks'; SELECT
  c.conname INTO owner_constraint FROM pg_constraint c WHERE c.conrelid = to_regclass('"ce_projects"') AND c.conindid =
  total_index AND c.contype IN ('u', 'p'); IF owner_constraint IS NULL THEN EXECUTE 'DROP INDEX ' \|\|
  total_index::regclass; ELSE EXECUTE format('ALTER TABLE "ce_projects" DROP CONSTRAINT %I', owner_constraint); END IF;
  ELSE CREATE UNIQUE INDEX IF NOT EXISTS "ix_projects_title_live" ON "ce_projects" ("tenant_id", lower("title")) WHERE
  "deleted_at" IS NULL; END IF; END $lc$`. Reason: B44 (ORML-30): the Up block found the total index by its bare name
  through search_path, so a total index of another table made it drop that table's index, and a live name held by
  another table made CREATE ... IF NOT EXISTS skip while the total index was dropped; both indexes are now read through
  pg_index.indrelid of the table, the total index is dropped by oid, and a live name held by another relation is
  refused.
- `generator-corpus-entities/executed-schema-contributions-and-registry (live-only Down block)`, Changed. Old text: `DO
  $lc$ DECLARE duplicate_key text; BEGIN PERFORM set_config('lock_timeout', '5s', true); IF
  coalesce(obj_description(to_regclass('"ix_projects_title_live"'), 'pg_class'), '') = '0400_helper_blocks' THEN SELECT
  concat_ws(', ', "tenant_id"::text, lower("title")::text) INTO duplicate_key FROM "ce_projects" WHERE "tenant_id" IS
  NOT NULL AND "title" IS NOT NULL GROUP BY "tenant_id", lower("title") HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS
  NOT NULL THEN RAISE EXCEPTION 'cannot recreate unique index uq_projects_title on ce_projects (tenant_id,
  lower(title)): duplicate key % exists, nothing was deleted', quote_literal(duplicate_key); END IF; CREATE UNIQUE INDEX
  IF NOT EXISTS "uq_projects_title" ON "ce_projects" ("tenant_id", lower("title")); COMMENT ON INDEX
  "ix_projects_title_live" IS NULL; END IF; END $lc$`. New text: `DO $lc$ DECLARE duplicate_key text; total_index oid;
  live_index oid; BEGIN PERFORM set_config('lock_timeout', '5s', true); SELECT ic.oid INTO live_index FROM pg_index i
  JOIN pg_class ic ON ic.oid = i.indexrelid WHERE i.indrelid = to_regclass('"ce_projects"') AND ic.relname =
  'ix_projects_title_live'; SELECT ic.oid INTO total_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
  WHERE i.indrelid = to_regclass('"ce_projects"') AND ic.relname = 'uq_projects_title'; IF
  coalesce(obj_description(live_index, 'pg_class'), '') = '0400_helper_blocks' THEN SELECT concat_ws(', ',
  "tenant_id"::text, lower("title")::text) INTO duplicate_key FROM "ce_projects" WHERE "tenant_id" IS NOT NULL AND
  "title" IS NOT NULL GROUP BY "tenant_id", lower("title") HAVING count(*) > 1 LIMIT 1; IF duplicate_key IS NOT NULL
  THEN RAISE EXCEPTION 'cannot recreate unique index uq_projects_title on ce_projects (tenant_id, lower(title)):
  duplicate key % exists, nothing was deleted', quote_literal(duplicate_key); END IF; IF total_index IS NULL AND EXISTS
  (SELECT 1 FROM pg_class x WHERE x.relname = 'uq_projects_title' AND x.relnamespace = (SELECT t.relnamespace FROM
  pg_class t WHERE t.oid = to_regclass('"ce_projects"'))) THEN RAISE EXCEPTION 'Index name % is already held by another
  relation in the schema of table %: PostgreSQL index names are schema-global, so creating it would be skipped and the
  table would stay without its index. Rename the index.', 'uq_projects_title', 'ce_projects'; END IF; CREATE UNIQUE
  INDEX IF NOT EXISTS "uq_projects_title" ON "ce_projects" ("tenant_id", lower("title")); EXECUTE format('COMMENT ON
  INDEX %s IS NULL', live_index::regclass); END IF; END $lc$`. Reason: B44 (ORML-30): the Down block read the live
  marker and dropped or released the live index by its bare name, and recreated the total index with IF NOT EXISTS under
  a name another table may hold; both indexes are now read through pg_index.indrelid of the table, the live index is
  released by oid, and a total name held by another relation is refused.
- `executed/soft-trash-existing-tables-are-cached-per-data-source, soft-trash-list-with-more-rows,
  soft-trash-list-with-a-project-filter-and-offset, soft-trash-list-with-no-listable-entry-runs-no-union and
  soft-trash-list-skips-missing-tables-and-keyless-entries`, Changed. Old text: `SELECT t FROM unnest(@tables::text[])
  AS t WHERE to_regclass(quote_ident(t)) IS NOT NULL`. New text: `SELECT t FROM unnest(@tables::text[]) AS t WHERE
  EXISTS (SELECT 1 FROM pg_class c WHERE c.oid = to_regclass(quote_ident(t)) AND c.relkind IN ('p', 'r'))`. Reason: B48
  (ORML-30): to_regclass also resolves a view, a materialized view, a foreign table, a sequence and an index, so the
  existing-tables lookup took them for tables; it now accepts only an ordinary or a partitioned table, which also drops
  foreign tables and materialized views from the answer. Parameters are unchanged.
- `executed/soft-restore-plan-* (6 goldens: of-a-batched-delete, of-a-single-update-history,
  of-an-unregistered-batch-member, root-already-live, root-deleted-but-not-locked, root-missing; the latest-version
  statement)`, Changed. Old text: `SELECT op, xact_id, changed_at FROM row_history WHERE source_table = @t AND row_id =
  @id ORDER BY id DESC LIMIT 1`. New text: `SELECT op, xact_id, changed_at FROM row_history WHERE source_table = @t AND
  row_id = @id AND EXISTS (SELECT 1 FROM "corpus_tasks" r WHERE r."id" = @id AND r."tenant_id" = @w) ORDER BY id DESC
  LIMIT 1`. Reason: ORML-24: the history reads of the revert and the restore filtered on the table and the row key
  alone, so a tenant could read the history, the existence of a version or the restore plan of another tenant's row;
  each now adds the live-row check of the entry's tenant column, AND EXISTS (SELECT 1 FROM "corpus_tasks" r WHERE r."id"
  = @id AND r."tenant_id" = @w), and binds the tenant as the new parameter @w (Guid
  10000000-0000-0000-0000-000000000001). Nothing else on the line moves.
- `executed/row-history-revert-newest-version, row-history-revert-newest-version-without-history-is-refused (the
  newest-version statement)`, Changed. Old text: `SELECT max(id) FROM row_history WHERE source_table = @t AND row_id =
  @id`. New text: `SELECT max(id) FROM row_history WHERE source_table = @t AND row_id = @id AND EXISTS (SELECT 1 FROM
  "corpus_tasks" r WHERE r."id" = @id AND r."tenant_id" = @w)`. Reason: ORML-24: the history reads of the revert and the
  restore filtered on the table and the row key alone, so a tenant could read the history, the existence of a version or
  the restore plan of another tenant's row; each now adds the live-row check of the entry's tenant column, AND EXISTS
  (SELECT 1 FROM "corpus_tasks" r WHERE r."id" = @id AND r."tenant_id" = @w), and binds the tenant as the new parameter
  @w (Guid 10000000-0000-0000-0000-000000000001). Nothing else on the line moves.
- `executed/row-history-revert-version-exists`, Changed. Old text: `SELECT EXISTS (SELECT 1 FROM row_history WHERE id =
  @k AND source_table = @t AND row_id = @id)`. New text: `SELECT EXISTS (SELECT 1 FROM row_history WHERE id = @k AND
  source_table = @t AND row_id = @id AND EXISTS (SELECT 1 FROM "corpus_tasks" r WHERE r."id" = @id AND r."tenant_id" =
  @w))`. Reason: ORML-24: the history reads of the revert and the restore filtered on the table and the row key alone,
  so a tenant could read the history, the existence of a version or the restore plan of another tenant's row; each now
  adds the live-row check of the entry's tenant column, AND EXISTS (SELECT 1 FROM "corpus_tasks" r WHERE r."id" = @id
  AND r."tenant_id" = @w), and binds the tenant as the new parameter @w (Guid 10000000-0000-0000-0000-000000000001).
  Nothing else on the line moves.
- `row-history/revert-sql-tenant-is-key-entity`, Changed. Old text: `WITH st(state) AS (SELECT (to_jsonb(live.*) \|\|
  coalesce((SELECT jsonb_object_agg(x.k, x.v) FROM (SELECT DISTINCT ON (e.k) e.k, e.v FROM row_history h CROSS JOIN
  LATERAL jsonb_each(h.old_values) AS e(k, v) WHERE h.source_table = @t AND h.row_id = @id AND h.id >= @k ORDER BY e.k,
  h.id) x), '{}'::jsonb)) - ARRAY[]::text[] FROM "corpus_projects" live WHERE live."id" = @id AND EXISTS (SELECT 1 FROM
  row_history v WHERE v.id = @k AND v.source_table = @t AND v.row_id = @id)) UPDATE "corpus_projects" AS t SET "name" =
  r."name" FROM st CROSS JOIN LATERAL jsonb_populate_record(NULL::"corpus_projects", st.state) AS r WHERE t."id" = @id
  AND t."deleted_at" IS NULL`. New text: `WITH st(state) AS (SELECT (to_jsonb(live.*) \|\| coalesce((SELECT
  jsonb_object_agg(x.k, x.v) FROM (SELECT DISTINCT ON (e.k) e.k, e.v FROM row_history h CROSS JOIN LATERAL
  jsonb_each(h.old_values) AS e(k, v) WHERE h.source_table = @t AND h.row_id = @id AND h.id >= @k ORDER BY e.k, h.id)
  x), '{}'::jsonb)) - ARRAY[]::text[] FROM "corpus_projects" live WHERE live."id" = @id AND live."id" = @w AND EXISTS
  (SELECT 1 FROM row_history v WHERE v.id = @k AND v.source_table = @t AND v.row_id = @id)) UPDATE "corpus_projects" AS
  t SET "name" = r."name" FROM st CROSS JOIN LATERAL jsonb_populate_record(NULL::"corpus_projects", st.state) AS r WHERE
  t."id" = @id AND t."id" = @w AND t."deleted_at" IS NULL`. Reason: ORML-24: the revert of an entity whose tenant column
  is also its key dropped the tenant predicate, while its lock kept it; one rule now filters every entry that is not
  Platform on its tenant column, key or not, so the live row read and the update both carry "id" = @w. The history read
  inside the statement and the SET list are unchanged.
- `query-read/group-by-having-and-order-by-aggregate`, Changed. Old text: `SELECT "status", count(*),
  COALESCE(SUM(COALESCE("views", 0)), 0) FROM "corpus_notes" GROUP BY "status" HAVING (count(*) > @p0 AND
  COALESCE(SUM(COALESCE("views", 0)), 0) <= @p1) ORDER BY count(*) DESC NULLS LAST, "status" ASC NULLS LAST LIMIT 5
  OFFSET 10`. New text: `SELECT "rank", count(*), COALESCE(SUM(COALESCE("views", 0)), 0) FROM "corpus_notes" GROUP BY
  "rank" HAVING (count(*) > @p0 AND COALESCE(SUM(COALESCE("views", 0)), 0) <= @p1) ORDER BY count(*) DESC NULLS LAST,
  "rank" ASC NULLS LAST LIMIT 5 OFFSET 10`. Reason: ORML-83: Min, Max and OrderByKey over an enum column are refused by
  name, because the database would rank the stored member names as text; the case grouped by the enum column Status and
  ordered by that group key, so its group key moved to the integer column rank. A change of the corpus case, not of the
  library output: the statement keeps its shape (count, sum, having, order, limit and offset).
- `query-read/where-closure-double-to-decimal`, Changed. Old text: `[0] Decimal 2.7`. New text: `[0] Decimal 2.5`.
  Reason: A change of the corpus case, not of the library output: the case captured the double 2.7, and (decimal)2.7 is
  runtime dependent (.NET 10 gives 2.7, .NET 11 gives the 28-digit expansion of the binary value), so it now captures
  the exactly representable 2.5. The statement text "price" = @p0 is unchanged.
- `generator-demo/executed-schema-contributions-and-registry`, Changed. Old text: `version 0000_baseline_OrmLiteDemo
  reversibility=Reversible down=none`. New text: `version 0000_baseline_OrmLiteDemo reversibility=Irreversible
  down=none`. Reason: ORML-54: the generated baseline entry was declared Reversible, so its derived Down dropped every
  managed table and the rows in it, and no down was ever exercised (section 202.7); it is declared Irreversible and
  RevertAsync refuses it by name. The text and the rest of the entry are unchanged.
- `generator-corpus-entities/executed-schema-contributions-and-registry`, Changed. Old text: `version
  0000_baseline_CorpusEntities reversibility=Reversible down=none`. New text: `version 0000_baseline_CorpusEntities
  reversibility=Irreversible down=none`. Reason: ORML-54: the generated baseline entry was declared Reversible, so its
  derived Down dropped every managed table and the rows in it, and no down was ever exercised (section 202.7); it is
  declared Irreversible and RevertAsync refuses it by name. The text and the rest of the entry are unchanged.
- `generator-golden-fixtures/executed-schema-contributions-and-registry`, Changed. Old text: `version
  0000_baseline_OrmLiteGoldenFixtures reversibility=Reversible down=none`. New text: `version
  0000_baseline_OrmLiteGoldenFixtures reversibility=Irreversible down=none`. Reason: ORML-54: the generated baseline
  entry was declared Reversible, so its derived Down dropped every managed table and the rows in it, and no down was
  ever exercised (section 202.7); it is declared Irreversible and RevertAsync refuses it by name. The text and the rest
  of the entry are unchanged.
- `generator-global-namespace/executed-schema-contributions-and-registry`, Changed. Old text: `version
  0000_baseline_CorpusGlobalNamespace reversibility=Reversible down=none`. New text: `version
  0000_baseline_CorpusGlobalNamespace reversibility=Irreversible down=none`. Reason: ORML-54: the generated baseline
  entry was declared Reversible, so its derived Down dropped every managed table and the rows in it, and no down was
  ever exercised (section 202.7); it is declared Irreversible and RevertAsync refuses it by name. The text and the rest
  of the entry are unchanged.
- `generator-tenant-option-default/executed-schema-contributions-and-registry`, Changed. Old text: `version
  0000_baseline_TenantOptionDefault reversibility=Reversible down=none`. New text: `version
  0000_baseline_TenantOptionDefault reversibility=Irreversible down=none`. Reason: ORML-54: the generated baseline entry
  was declared Reversible, so its derived Down dropped every managed table and the rows in it, and no down was ever
  exercised (section 202.7); it is declared Irreversible and RevertAsync refuses it by name. The text and the rest of
  the entry are unchanged.
- `generator-tenant-option-invalid/executed-schema-contributions-and-registry`, Changed. Old text: `version
  0000_baseline_TenantOptionInvalid reversibility=Reversible down=none`. New text: `version
  0000_baseline_TenantOptionInvalid reversibility=Irreversible down=none`. Reason: ORML-54: the generated baseline entry
  was declared Reversible, so its derived Down dropped every managed table and the rows in it, and no down was ever
  exercised (section 202.7); it is declared Irreversible and RevertAsync refuses it by name. The text and the rest of
  the entry are unchanged.
- `generator-tenant-option-scope-id/executed-schema-contributions-and-registry`, Changed. Old text: `version
  0000_baseline_TenantOptionScope reversibility=Reversible down=none`. New text: `version
  0000_baseline_TenantOptionScope reversibility=Irreversible down=none`. Reason: ORML-54: the generated baseline entry
  was declared Reversible, so its derived Down dropped every managed table and the rows in it, and no down was ever
  exercised (section 202.7); it is declared Irreversible and RevertAsync refuses it by name. The text and the rest of
  the entry are unchanged.

## Entries per group (derived from the capture)

| group | entries |
| --- | --- |
| `constants` | 1 |
| `ddl-builder` | 36 |
| `ddl-helpers` | 36 |
| `dialect` | 9 |
| `executed` | 96 |
| `generator-corpus-entities` | 8 |
| `generator-demo` | 8 |
| `generator-diagnostics` | 4 |
| `generator-global-namespace` | 8 |
| `generator-golden-fixtures` | 8 |
| `generator-tenant-option-default` | 6 |
| `generator-tenant-option-invalid` | 6 |
| `generator-tenant-option-scope-id` | 6 |
| `hash` | 4 |
| `json` | 40 |
| `locks-intervals` | 20 |
| `query-insert-many` | 7 |
| `query-read` | 127 |
| `query-upsert` | 21 |
| `query-write` | 30 |
| `refusals` | 144 |
| `row-history` | 44 |
| `soft-delete` | 12 |
| total | 681 |

## What the groups hold

- `query-read`, `query-write`, `query-upsert`, `query-insert-many`, `soft-delete`, `json`, `locks-intervals`: the SQL text and the ordered parameter list that each `Query<T>.Render*` method returns, for the lambda DSL and the `Db.*` markers, including soft-delete live terms, conflict targets, RETURNING, FILTER, row locks, intervals, array parameters and every JSON clause.
- `refusals`: the exception type and message of every guard reachable from the render path, the capability refusals of a dialect that supports nothing, and the value-type guards.
- `dialect`: each `ISqlDialect` member called directly, the LIKE escape helper, the DDL dialect and the parameter conversions of `AddParam`.
- `ddl-builder`, `ddl-helpers`: every `MigrationBuilder` and `TableBuilder` operation, and the DO-block helpers (`ForeignKeyActionDdl`, `LiveOnlyConversionDdl`, `SoftDeleteColumnDdl`) with their refusals.
- `row-history`, `hash`: the history DDL (preamble, capture function, per-table triggers), the history read, revert and trash statements, and the hash formulas that name migration versions and trigger markers.
- `executed`: transcripts of the statements that library code builds inside async methods and runs, recorded through a scripted ADO.NET provider (statement text, parameter names and values, connection, transaction brackets). No database is involved.
- `constants`: every string constant and static read-only string of the surveyed types, private ones included.
- `generator-*`: for each fixture source set, every generated file, the generator diagnostics, whether the generated code compiles against the runtime, and a dump of the generated schema constants, migration contributions (version names, operations, reversibility) and registry entries read from the compiled generated assembly. The sets that run the migration runner also record the generated migration runner applied to a scripted database, fresh and partially applied.

## Coverage of the statement-producing surface (derived by `CorpusIndex`)

Method: the surface is every public method (and, for the types marked internal below, every internal method) of the surveyed Runtime types, plus the getters of `Query<T>` and public static getters. A member counts as referenced when the IL of the code under `Cases/` calls it, constructs it or takes its token (the `Db.*` markers appear as tokens inside expression trees). A member that no case references and no exclusion explains makes the capture fail, so the table cannot silently shrink.

| type | members | referenced | excluded |
| --- | --- | --- | --- |
| `AdvisorySessionLocks` | 3 | 3 | 0 |
| `ConflictGuard` | 2 | 0 | 2 |
| `ConstraintColumns` | 2 | 1 | 1 |
| `Db` | 12 | 12 | 0 |
| `DbActor` | 4 | 4 | 0 |
| `DbExtensions` | 1 | 1 | 0 |
| `DbLane` | 2 | 2 | 0 |
| `ForeignKeyActionDdl` | 3 | 3 | 0 |
| `GatedDataSource` | 1 | 1 | 0 |
| `InsertManyChunks` | 2 | 1 | 1 |
| `LikePattern` | 4 | 4 | 0 |
| `LiveOnlyConversionDdl` | 2 | 2 | 0 |
| `Migration` | 2 | 2 | 0 |
| `MigrationBuilder` | 25 | 21 | 4 |
| `OrmContext` | 5 | 2 | 3 |
| `OrmEnum` | 2 | 2 | 0 |
| `OrmLiteSettings` | 3 | 3 | 0 |
| `PgDdlDialect` | 3 | 3 | 0 |
| `PgDialect` | 2 | 2 | 0 |
| `Query` | 108 | 95 | 13 |
| `Row` | 3 | 3 | 0 |
| `RowHistoryDdl` | 10 | 7 | 3 |
| `RowHistoryRead` | 6 | 6 | 0 |
| `RowHistoryRevert` | 6 | 6 | 0 |
| `SoftDeleteColumnDdl` | 2 | 2 | 0 |
| `SoftDeleteSql` | 3 | 3 | 0 |
| `SoftRestore` | 3 | 3 | 0 |
| `SoftTrashRead` | 5 | 5 | 0 |
| `TableBuilder` | 6 | 5 | 1 |
| `UnitOfWork` | 5 | 5 | 0 |
| `UpsertSet` | 7 | 7 | 0 |
| total | 244 | 216 | 28 |

Surveyed types that are internal: `RowHistoryDdl`, `InsertManyChunks`, `ConflictGuard`.

### Excluded members and why

| member | reason |
| --- | --- |
| `ConflictGuard.RequireDefined/1` | internal guard reached through Query.RenderUpsert; the message is pinned by refusals/upsert-with-an-undefined-conflict-action |
| `ConflictGuard.RequireTarget/2` | internal guard reached through Query.RenderUpsert; the message is pinned by refusals/upsert-update-without-a-target |
| `ConstraintColumns..ctor/2` | record constructor of the result type; the type emits no text, ConstraintColumns.ForAsync is pinned by the executed/constraint-columns-* transcripts |
| `InsertManyChunks.RunAsync/5` | internal runner reached only through Query.InsertManyAsync; its transcripts are the executed/insert-many-* entries |
| `MigrationBuilder.Add/1` | appends an already built operation node; the nodes it receives are the ones the typed methods build, pinned by Nodes/DdlOperationTests |
| `MigrationBuilder.AddDecimalColumn/6` | typed decimal column; the corpus predates typed columns, text pinned by Nodes/DdlOperationTests |
| `MigrationBuilder.DropForeignKey/2` | typed inverse of AddForeignKey added with the reversibility classifier, pinned by Nodes/MigrationReversibilityTests |
| `MigrationBuilder.Render/1` | renders the recorded nodes for an explicit provider; the same compile path as Operations, pinned by Nodes/DdlOperationTests |
| `OrmContext.DisposeAsync/0` | releases the data sources an options-built context owns; it emits no text |
| `OrmContext.MigrateAsync/1` | applies the contributions the generated assemblies registered through MigrationRunner.RunAsync, whose transcripts are the generator-*/migration-runner-* entries; MigrateAsyncTests apply them |
| `OrmContext.MigrateAsync/2` | forwards explicit contributions to MigrationRunner.RunAsync, whose transcripts are the generator-*/migration-runner-* entries; MigrateAsyncTests apply them |
| `Query.BuildAggregates/0` | internal tree builder beneath CompileAggregates; the corpus pins the text it compiles to and QueryTreeTests pin the tree |
| `Query.BuildProject/1` | internal tree builder beneath CompileProject; the corpus pins the text it compiles to and QueryTreeTests pin the tree |
| `Query.BuildProjection/0` | internal tree builder beneath CompileProjection; the corpus pins the text it compiles to and QueryTreeTests pin the tree |
| `Query.BuildScalar/1` | internal tree builder beneath CompileScalar; the clause-free text is pinned by the executed query-scalar-soft and query-scalar-null-answer-is-the-default entries and the ordered text by ScalarRenderingTests |
| `Query.BuildSelect/0` | internal tree builder beneath CompileSelect; the corpus pins the text it compiles to and QueryTreeTests pin the tree |
| `Query.BuildView/2` | internal tree builder beneath AsView; the corpus pins the CREATE VIEW text it compiles to in ddl-builder/create-view-* and view-over-a-join-and-an-aggregate |
| `Query.CompileScalar/1` | the statement ScalarAsync runs; the clause-free text is pinned by the executed query-scalar-soft and query-scalar-null-answer-is-the-default entries, which run through it, and the order and offset text on both providers by ScalarRenderingTests |
| `Query.Definition/1` | internal factory of the provider-bound query that MigrationBuilder.From returns; its use is pinned by every ddl-builder view entry |
| `Query.RenderScalar/1` | text of CompileScalar; pinned by the same executed entries and by ScalarRenderingTests on both providers |
| `Query.get_SourceTable/0` | internal accessor read only by ViewColumns; the table it names is pinned by ddl-builder/create-view-over-a-filtered-query |
| `Query.get_State/0` | internal accessor read only by ViewColumns; the scope it carries is pinned by ddl-builder/view-over-a-join-and-an-aggregate |
| `Query.get_Translator/0` | internal accessor read only by ViewColumns; the predicates it translates are pinned by ddl-builder/view-over-a-join-and-an-aggregate |
| `Query.get_WhereTerms/0` | node-tree accessor beside WhereClauses; the terms it exposes are the ones WhereClauses renders, pinned by query-read/select-render-is-repeatable-and-exposes-the-where-terms |
| `RowHistoryDdl.Install/1` | node factory beneath Reconcile; the statements it lowers to are pinned by the row-history/ddl-reconcile-* entries |
| `RowHistoryDdl.SetHashWith/2` | the legacy-or-structural identity beneath SetHash; the legacy form is pinned by hash/set-hash-vectors and the structural form by SqliteRules/SqliteHistoryIdentityTests |
| `RowHistoryDdl.get_Preamble/0` | property that replaced a static readonly field so no provider is needed at type initialisation; its text is pinned by row-history/ddl-preamble-under-the-default-actor-setting |
| `TableBuilder.DecimalColumn/6` | typed decimal column; the corpus predates typed columns, text pinned by Nodes/DdlOperationTests |

### `ISqlDialect` members, observed through a forwarding dialect

Every case runs against a dialect that forwards to `PgDialect` and records which member was called and by which group. Capability flags that the query code reads only to refuse appear in the `refusals` group.

- members of the interface: 49
- reached from query, DDL or executed paths: 13
- reached only by the direct calls of the `dialect` group: 36 (`AnyOfExpression`, `CoalesceAssignment`, `ComparableParameter`, `ConflictClause`, `CountAllExpression`, `CountDistinctExpression`, `CountFilterExpression`, `IncrementAssignment`, `JsonArrayContainsAnyExpression`, `JsonArrayContainsStringExpression`, `JsonDocumentMergeAssignment`, `JsonDocumentParameter`, `JsonDocumentRemoveThenMergeAssignment`, `JsonKeyComparableExpression`, `JsonKeyExistsExpression`, `JsonKeyIndexTextExpression`, `JsonKeyIndexValueExpression`, `JsonKeyOrderTerm`, `JsonKeyRankExpression`, `JsonKeyTextExpression`, `JsonMemberIncrementExpression`, `JsonNullValueExpression`, `JsonObjectAssignment`, `KeyPreservingRowLockClause`, `MinExpression`, `MovedFieldNumberOrEpochMillisOrderTerm`, `MovedFieldResolvedTextExpression`, `NowExpression`, `NowPlusMinutesExpression`, `NullSafeEqualsExpression`, `OrderDirectionClause`, `Page`, `ReturningClause`, `RowLockClause`, `SumCoalescedExpression`, `TextValueExpression`)
- never reached: 0

### Constants

- string constants and static read-only strings captured: 20

### Generator

| set | generated files | diagnostics | executed |
| --- | --- | --- | --- |
| `generator-corpus-entities` | 3 | 1 | yes |
| `generator-demo` | 3 | 0 | yes |
| `generator-diagnostics` | 3 | 28 | no |
| `generator-global-namespace` | 3 | 0 | yes |
| `generator-golden-fixtures` | 3 | 0 | yes |
| `generator-tenant-option-default` | 3 | 0 | yes |
| `generator-tenant-option-invalid` | 3 | 1 | yes |
| `generator-tenant-option-scope-id` | 3 | 0 | yes |

## Not covered, by design

- Execution against a PostgreSQL server: the goldens fix text and parameters, not what a server does with them. Server behaviour (jsonb operators, triggers, locks, advisory locks, `pg_input_is_valid`) stays proven only by the host's own Postgres suites.
- Provider-side behaviour of a real ADO.NET driver: array binding, parameter type inference, the pooled connection reset. The scripted provider returns what a case tells it to.
- Rows read back and mapped into entities: the cases map no rows except where a transcript needs one.
- Output that depends on the process rather than on the library: the actor setting other than the library default, `RowHistoryDdl.Preamble` as a process static, and timing.
- Compile-time behaviour of the generated code under AOT: generated sources are compiled against the runtime and read back, never published.
- The public API shape itself (signatures, accessibility): a later slice that changes it changes the cases and says so; only the text produced is pinned.
- Members listed under Excluded above, each with its reason.
