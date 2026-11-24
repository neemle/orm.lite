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
  UnmappedType.Span) say: 'Add a mapping to OrmGenerator.Classify, or type the property as Neemle.OrmLite.Jsonb to store
  it as a jsonb column.'`. New text: `the two ORMLITE001 lines say: 'Use a supported type (string, int, long, bool,
  double, decimal, Guid, DateTime, DateOnly or an enum, each also nullable), type the property as Neemle.OrmLite.Jsonb
  to store it as a jsonb column, or mark it [NotAColumn] to leave it out.'; every other line is unchanged`. Reason: B29
  (ORML-22): the old text pointed at a method of the generator source, which a user of the package cannot change; the
  message now names what the user can do. The property, location and the rest of the text are unchanged.
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
- `dialect/identifiers-parameters-and-paging`, Removed. Old text: `Quote, Param, Page, MaxBindParameters and
  PingStatement of PgDialect`. New text: (removed). Reason: ORML-21 slice 4: the legacy dialect path is removed from the
  never-published 0.1.0-pre without a shim; this case pinned only that API. The same text is rendered by the provider's
  serializer, which the Nodes and provider rules tests pin.
- `dialect/capability-flags`, Removed. Old text: `the nine Supports flags of PgDialect`. New text: (removed). Reason:
  ORML-21 slice 4: the legacy dialect path is removed from the never-published 0.1.0-pre without a shim; this case
  pinned only that API. The same text is rendered by the provider's serializer, which the Nodes and provider rules tests
  pin.
- `dialect/fixed-fragments`, Removed. Old text: `NowExpression, the row lock clauses, CountAllExpression and
  JsonNullValueExpression`. New text: (removed). Reason: ORML-21 slice 4: the legacy dialect path is removed from the
  never-published 0.1.0-pre without a shim; this case pinned only that API. The same text is rendered by the provider's
  serializer, which the Nodes and provider rules tests pin.
- `dialect/clauses-and-aggregates`, Removed. Old text: `the returning, coalesce, any-of, null-safe, count, sum, min,
  interval, increment and order members`. New text: (removed). Reason: ORML-21 slice 4: the legacy dialect path is
  removed from the never-published 0.1.0-pre without a shim; this case pinned only that API. The same text is rendered
  by the provider's serializer, which the Nodes and provider rules tests pin.
- `dialect/conflict-clause`, Removed. Old text: `ConflictClause of PgDialect for every upsert term form`. New text:
  (removed). Reason: ORML-21 slice 4: the legacy dialect path is removed from the never-published 0.1.0-pre without a
  shim; this case pinned only that API. The same text is rendered by the provider's serializer, which the Nodes and
  provider rules tests pin.
- `dialect/json-fragments`, Removed. Old text: `the JSON members of PgDialect`. New text: (removed). Reason: ORML-21
  slice 4: the legacy dialect path is removed from the never-published 0.1.0-pre without a shim; this case pinned only
  that API. The same text is rendered by the provider's serializer, which the Nodes and provider rules tests pin.
- `dialect/ddl-dialect`, Removed. Old text: `Quote and Literal of PgDdlDialect`. New text: (removed). Reason: ORML-21
  slice 4: the legacy dialect path is removed from the never-published 0.1.0-pre without a shim; this case pinned only
  that API. The same text is rendered by the provider's serializer, which the Nodes and provider rules tests pin.
- `dialect/like-pattern`, Removed. Old text: `Escape, EscapeChar, EscapeClause, EscapeLiteral, Contains, StartsWith and
  EndsWith of LikePattern`. New text: (removed). Reason: ORML-21 slice 4: the case pinned a helper that is not part of
  the dialect path; its assertions moved to a plain test with the same expected text, and the helper's members are
  listed as excluded in the coverage table.
- `dialect/db-extensions-add-param-conversions`, Removed. Old text: `the parameter conversions of AddParam`. New text:
  (removed). Reason: ORML-21 slice 4: the case pinned a helper that is not part of the dialect path; its assertions
  moved to a plain test with the same expected text, and the helper's members are listed as excluded in the coverage
  table.
- `refusals/dialect-json-value-order-without-a-sql-type`, Removed. Old text: `PgDialect refuses a JsonValueOrder it has
  no SQL type for`. New text: (removed). Reason: ORML-21 slice 4: the legacy dialect path is removed from the
  never-published 0.1.0-pre without a shim; this case pinned only that API. The same text is rendered by the provider's
  serializer, which the Nodes and provider rules tests pin.
- `refusals/dialect-rank-without-options`, Removed. Old text: `PgDialect refuses a rank over no options`. New text:
  (removed). Reason: ORML-21 slice 4: the legacy dialect path is removed from the never-published 0.1.0-pre without a
  shim; this case pinned only that API. The same text is rendered by the provider's serializer, which the Nodes and
  provider rules tests pin.
- `json/jsonb-default-document-and-column-type`, Removed. Old text: `[0] jsonb and [1] '{}'::jsonb, the values of
  Jsonb.ColumnType and Jsonb.EmptyDocumentDefaultSql`. New text: (removed). Reason: ORML-21 slice 4: the two Jsonb
  constants were read only by the removed PostgreSQL dialect, so they are removed; the jsonb type and the empty-document
  default are rendered by the provider's serializer and pinned by the ddl-builder entries.
- `constants/runtime-string-constants`, Changed. Old text: `[6] Jsonb.ColumnType = "jsonb" and [7]
  Jsonb.EmptyDocumentDefaultSql = "'{}'::jsonb"`. New text: `(both lines are gone and every later line moves up two
  places)`. Reason: ORML-21 slice 4: the two Jsonb constants were read only by the removed PostgreSQL dialect, so they
  are removed; the jsonb type and the empty-document default are rendered by the provider's serializer and pinned by the
  ddl-builder entries.
- `refusals/capability-* (23 entries) and refusals/group-by-count-where-without-the-filter-capability`, Changed. Old
  text: `type: System.NotSupportedException; message: NoCapabilityDialect has no row lock; ..., the sentence after the
  dialect name differing per capability`. New text: `type: Neemle.OrmLite.CapabilityNotSupportedException; message:
  provider 'no-capability' does not support RowLock, which ForUpdate requires (table 'corpus_notes'). no-capability has
  no row lock; ..., the same sentence as before`. Reason: ORML-21 slice 4: the refusal cases ran against a dialect that
  supported nothing, which threw a plain NotSupportedException worded by the dialect. They now run against a provider
  that supports nothing, so the refusal is the CapabilityNotSupportedException the provider path always threw, naming
  the provider, the capability, the operation and the table, followed by the same guidance sentence as before.
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
- `hash/set-hash-vectors`, Changed. Old text: `no tables = 55b686441ec82b88; tenant table = e4071fdc5c0927f5; platform
  table = 28d441b68fac0005; tenant then platform = 3525748bf40af974; platform then tenant = 7806d68ad12060f7; version
  name formula = 9900_orm_history_reconcile_Assembly_e4071fdc5c0927f5`. New text: `no tables = e3b0c44298fc1c14; tenant
  table = 991e2994cc4b646b; platform table = 30a6a6da8c76b2a8; tenant then platform = f8c47a43002b5b36; platform then
  tenant = b6f6731be62a61a9; version name formula = 9900_orm_history_reconcile_Assembly_991e2994cc4b646b`. Reason:
  ORML-21 S5 (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered create statements and the
  history set hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL provider was loaded, and a
  structural hash otherwise, so the 9900_ version names depended on what was loaded. Both are now the structural hash
  only, in the core: the index set over table, index name, uniqueness, live column, columns and shape, the history set
  over the table names and their argument hashes and, when it is not the library default, the actor setting name (the
  corpus runs under the default). A database that carries the old names runs the renamed reconcile and history installs
  once (idempotent) and records one new schema_migrations row for each. Nothing else on the line moves.
- `hash/set-hash-equals-statements-hash-of-reconcile`, Changed. Old text: `e4071fdc5c0927f5 == e4071fdc5c0927f5 : True`.
  New text: `991e2994cc4b646b == e4071fdc5c0927f5 : False`. Reason: ORML-21 S5 (owner decision D1): the index-set hash
  was the hash of the PostgreSQL-rendered create statements and the history set hash the hash of the PostgreSQL-rendered
  install, whenever the PostgreSQL provider was loaded, and a structural hash otherwise, so the 9900_ version names
  depended on what was loaded. Both are now the structural hash only, in the core: the index set over table, index name,
  uniqueness, live column, columns and shape, the history set over the table names and their argument hashes and, when
  it is not the library default, the actor setting name (the corpus runs under the default). A database that carries the
  old names runs the renamed reconcile and history installs once (idempotent) and records one new schema_migrations row
  for each. Nothing else on the line moves. The hash of the rendered reconcile statements, StatementsHash, is unchanged;
  the set hash no longer equals it, which is the point of the change.
- `generator-demo/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (the 9900_ version names and the set hashes)`, Changed. Old text:
  `9900_orm_index_reconcile_OrmLiteDemo_d27e299df0226960; IndexSetHash: d27e299df0226960;
  9900_orm_history_reconcile_OrmLiteDemo_e9dbcb569c4fbc15; HistorySetHash: e9dbcb569c4fbc15`. New text:
  `9900_orm_index_reconcile_OrmLiteDemo_a7104bccd4193080; IndexSetHash: a7104bccd4193080;
  9900_orm_history_reconcile_OrmLiteDemo_6df7f955b08f5c73; HistorySetHash: 6df7f955b08f5c73`. Reason: ORML-21 S5 (owner
  decision D1): the index-set hash was the hash of the PostgreSQL-rendered create statements and the history set hash
  the hash of the PostgreSQL-rendered install, whenever the PostgreSQL provider was loaded, and a structural hash
  otherwise, so the 9900_ version names depended on what was loaded. Both are now the structural hash only, in the core:
  the index set over table, index name, uniqueness, live column, columns and shape, the history set over the table names
  and their argument hashes and, when it is not the library default, the actor setting name (the corpus runs under the
  default). A database that carries the old names runs the renamed reconcile and history installs once (idempotent) and
  records one new schema_migrations row for each. Nothing else on the line moves.
- `generator-corpus-entities/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (the 9900_ version names and the set hashes)`, Changed. Old text:
  `9900_orm_index_reconcile_CorpusEntities_e3aab199192c293c; IndexSetHash: e3aab199192c293c;
  9900_orm_history_reconcile_CorpusEntities_75a913ef24f4eca1; HistorySetHash: 75a913ef24f4eca1`. New text:
  `9900_orm_index_reconcile_CorpusEntities_e5ff1a2ec4afdb56; IndexSetHash: e5ff1a2ec4afdb56;
  9900_orm_history_reconcile_CorpusEntities_e61f568d5478fd36; HistorySetHash: e61f568d5478fd36`. Reason: ORML-21 S5
  (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered create statements and the history set
  hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL provider was loaded, and a structural hash
  otherwise, so the 9900_ version names depended on what was loaded. Both are now the structural hash only, in the core:
  the index set over table, index name, uniqueness, live column, columns and shape, the history set over the table names
  and their argument hashes and, when it is not the library default, the actor setting name (the corpus runs under the
  default). A database that carries the old names runs the renamed reconcile and history installs once (idempotent) and
  records one new schema_migrations row for each. Nothing else on the line moves.
- `generator-golden-fixtures/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (the 9900_ version names and the set hashes)`, Changed. Old text:
  `9900_orm_index_reconcile_OrmLiteGoldenFixtures_eb6e32bc81a12870; IndexSetHash: eb6e32bc81a12870`. New text:
  `9900_orm_index_reconcile_OrmLiteGoldenFixtures_cf6209a70c2a593c; IndexSetHash: cf6209a70c2a593c`. Reason: ORML-21 S5
  (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered create statements and the history set
  hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL provider was loaded, and a structural hash
  otherwise, so the 9900_ version names depended on what was loaded. Both are now the structural hash only, in the core:
  the index set over table, index name, uniqueness, live column, columns and shape, the history set over the table names
  and their argument hashes and, when it is not the library default, the actor setting name (the corpus runs under the
  default). A database that carries the old names runs the renamed reconcile and history installs once (idempotent) and
  records one new schema_migrations row for each. Nothing else on the line moves.
- `generator-global-namespace/migration-runner-fresh-database, migration-runner-partially-applied and
  executed-schema-contributions-and-registry (the 9900_ version names and the set hashes)`, Changed. Old text:
  `9900_orm_index_reconcile_CorpusGlobalNamespace_cdf8e7986621fc5e; IndexSetHash: cdf8e7986621fc5e;
  9900_orm_history_reconcile_CorpusGlobalNamespace_0aa8a0d2f203193f; HistorySetHash: 0aa8a0d2f203193f`. New text:
  `9900_orm_index_reconcile_CorpusGlobalNamespace_ae551789d8c9818c; IndexSetHash: ae551789d8c9818c;
  9900_orm_history_reconcile_CorpusGlobalNamespace_1a3c33d971c4c2ce; HistorySetHash: 1a3c33d971c4c2ce`. Reason: ORML-21
  S5 (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered create statements and the history
  set hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL provider was loaded, and a structural
  hash otherwise, so the 9900_ version names depended on what was loaded. Both are now the structural hash only, in the
  core: the index set over table, index name, uniqueness, live column, columns and shape, the history set over the table
  names and their argument hashes and, when it is not the library default, the actor setting name (the corpus runs under
  the default). A database that carries the old names runs the renamed reconcile and history installs once (idempotent)
  and records one new schema_migrations row for each. Nothing else on the line moves.
- `generator-tenant-option-default/executed-schema-contributions-and-registry (the 9900_ version names and the set
  hashes)`, Changed. Old text: `9900_orm_history_reconcile_TenantOptionDefault_58a560a7fa6d2d7f; HistorySetHash:
  58a560a7fa6d2d7f`. New text: `9900_orm_history_reconcile_TenantOptionDefault_99ac1720f8990e2b; HistorySetHash:
  99ac1720f8990e2b`. Reason: ORML-21 S5 (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered
  create statements and the history set hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL
  provider was loaded, and a structural hash otherwise, so the 9900_ version names depended on what was loaded. Both are
  now the structural hash only, in the core: the index set over table, index name, uniqueness, live column, columns and
  shape, the history set over the table names and their argument hashes and, when it is not the library default, the
  actor setting name (the corpus runs under the default). A database that carries the old names runs the renamed
  reconcile and history installs once (idempotent) and records one new schema_migrations row for each. Nothing else on
  the line moves.
- `generator-tenant-option-invalid/executed-schema-contributions-and-registry (the 9900_ version names and the set
  hashes)`, Changed. Old text: `9900_orm_history_reconcile_TenantOptionInvalid_58a560a7fa6d2d7f; HistorySetHash:
  58a560a7fa6d2d7f`. New text: `9900_orm_history_reconcile_TenantOptionInvalid_99ac1720f8990e2b; HistorySetHash:
  99ac1720f8990e2b`. Reason: ORML-21 S5 (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered
  create statements and the history set hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL
  provider was loaded, and a structural hash otherwise, so the 9900_ version names depended on what was loaded. Both are
  now the structural hash only, in the core: the index set over table, index name, uniqueness, live column, columns and
  shape, the history set over the table names and their argument hashes and, when it is not the library default, the
  actor setting name (the corpus runs under the default). A database that carries the old names runs the renamed
  reconcile and history installs once (idempotent) and records one new schema_migrations row for each. Nothing else on
  the line moves.
- `generator-tenant-option-scope-id/executed-schema-contributions-and-registry (the 9900_ version names and the set
  hashes)`, Changed. Old text: `9900_orm_history_reconcile_TenantOptionScope_4ebf33eaebd07b21; HistorySetHash:
  4ebf33eaebd07b21`. New text: `9900_orm_history_reconcile_TenantOptionScope_b26c64fa058bdef5; HistorySetHash:
  b26c64fa058bdef5`. Reason: ORML-21 S5 (owner decision D1): the index-set hash was the hash of the PostgreSQL-rendered
  create statements and the history set hash the hash of the PostgreSQL-rendered install, whenever the PostgreSQL
  provider was loaded, and a structural hash otherwise, so the 9900_ version names depended on what was loaded. Both are
  now the structural hash only, in the core: the index set over table, index name, uniqueness, live column, columns and
  shape, the history set over the table names and their argument hashes and, when it is not the library default, the
  actor setting name (the corpus runs under the default). A database that carries the old names runs the renamed
  reconcile and history installs once (idempotent) and records one new schema_migrations row for each. Nothing else on
  the line moves.
- `refusals/predicate-json-array-contains-string-null-value`, Changed. Old text: `message: Db.JsonArrayContainsString
  tests for exact-string array membership, and a null value would bind SQL NULL -- jsonb_exists against a NULL argument
  matches no row at all rather than refusing. (Parameter 'value')`. New text: `message: Db.JsonArrayContainsString tests
  for exact-string array membership, and a null value would bind SQL NULL -- a JSON array-membership test against a NULL
  argument matches no row at all rather than refusing. (Parameter 'value')`. Reason: ORML-21 S6: the message is written
  in the provider-neutral runtime and named the PostgreSQL function jsonb_exists, which is provider SQL text in the
  core; it now names the JSON array-membership test. The exception type, the parameter name and the rest of the sentence
  are unchanged.
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
| `json` | 39 |
| `locks-intervals` | 20 |
| `query-insert-many` | 7 |
| `query-read` | 127 |
| `query-upsert` | 21 |
| `query-write` | 30 |
| `refusals` | 142 |
| `row-history` | 44 |
| `soft-delete` | 12 |
| total | 669 |

## What the groups hold

- `query-read`, `query-write`, `query-upsert`, `query-insert-many`, `soft-delete`, `json`, `locks-intervals`: the SQL text and the ordered parameter list that each `Query<T>.Render*` method returns, for the lambda DSL and the `Db.*` markers, including soft-delete live terms, conflict targets, RETURNING, FILTER, row locks, intervals, array parameters and every JSON clause.
- `refusals`: the exception type and message of every guard reachable from the render path, the capability refusals of a provider that supports nothing, and the value-type guards.
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
| `DbActor` | 2 | 2 | 0 |
| `DbExtensions` | 1 | 0 | 1 |
| `DbLane` | 2 | 2 | 0 |
| `ForeignKeyActionDdl` | 3 | 3 | 0 |
| `GatedDataSource` | 1 | 1 | 0 |
| `InsertManyChunks` | 2 | 1 | 1 |
| `LikePattern` | 4 | 0 | 4 |
| `LiveOnlyConversionDdl` | 2 | 2 | 0 |
| `Migration` | 2 | 2 | 0 |
| `MigrationBuilder` | 24 | 21 | 3 |
| `OrmContext` | 5 | 2 | 3 |
| `OrmEnum` | 2 | 2 | 0 |
| `OrmLiteSettings` | 2 | 2 | 0 |
| `Query` | 108 | 95 | 13 |
| `Row` | 3 | 3 | 0 |
| `RowHistoryDdl` | 10 | 7 | 3 |
| `RowHistoryRead` | 6 | 6 | 0 |
| `RowHistoryRevert` | 6 | 6 | 0 |
| `SoftDeleteColumnDdl` | 2 | 2 | 0 |
| `SoftDeleteSql` | 3 | 3 | 0 |
| `SoftRestore` | 3 | 3 | 0 |
| `SoftTrashRead` | 5 | 5 | 0 |
| `TableBuilder` | 5 | 4 | 1 |
| `UnitOfWork` | 5 | 5 | 0 |
| `UpsertSet` | 7 | 5 | 2 |
| total | 234 | 200 | 34 |

Surveyed types that are internal: `RowHistoryDdl`, `InsertManyChunks`, `ConflictGuard`.

### Excluded members and why

| member | reason |
| --- | --- |
| `ConflictGuard.RequireDefined/1` | internal guard reached through Query.RenderUpsert; the message is pinned by refusals/upsert-with-an-undefined-conflict-action |
| `ConflictGuard.RequireTarget/2` | internal guard reached through Query.RenderUpsert; the message is pinned by refusals/upsert-update-without-a-target |
| `ConstraintColumns..ctor/2` | record constructor of the result type; the type emits no text, ConstraintColumns.ForAsync is pinned by the executed/constraint-columns-* transcripts |
| `DbExtensions.AddParam/3` | converts a value to the ADO.NET parameter it binds and emits no statement; pinned by DbExtensionsAddParamTests |
| `InsertManyChunks.RunAsync/5` | internal runner reached only through Query.InsertManyAsync; its transcripts are the executed/insert-many-* entries |
| `LikePattern.Contains/1` | wraps EscapeLiteral in the percent signs of a LIKE pattern, pinned by LikePatternTests |
| `LikePattern.EndsWith/1` | prepends the percent sign to EscapeLiteral, pinned by LikePatternTests |
| `LikePattern.EscapeLiteral/1` | escape helper beneath every Contains, StartsWith and EndsWith predicate; its text is pinned by LikePatternTests |
| `LikePattern.StartsWith/1` | appends the percent sign to EscapeLiteral, pinned by LikePatternTests |
| `MigrationBuilder.Add/1` | appends an already built operation node; the nodes it receives are the ones the typed methods build, pinned by Nodes/DdlOperationTests |
| `MigrationBuilder.AddDecimalColumn/6` | typed decimal column; the corpus predates typed columns, text pinned by Nodes/DdlOperationTests |
| `MigrationBuilder.DropForeignKey/2` | typed inverse of AddForeignKey added with the reversibility classifier, pinned by Nodes/MigrationReversibilityTests |
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
| `RowHistoryDdl.Preamble/1` | public form of PreambleFor under the library default actor setting, for the provider it is given; its text is pinned by row-history/ddl-preamble-under-the-default-actor-setting |
| `RowHistoryDdl.SetHash/2` | the form of SetHash that names the actor setting; under the library default it equals SetHash, which the hash/set-hash-vectors entry pins, and a custom setting is pinned by Ports.Tests/MigrationIdentityTests |
| `TableBuilder.DecimalColumn/6` | typed decimal column; the corpus predates typed columns, text pinned by Nodes/DdlOperationTests |
| `UpsertSet.ToJsonDocumentParam/2` | data constructor of an upsert term for a JSON document; the term renders no text, the upsert text is pinned by the query-upsert entries |
| `UpsertSet.ToParam/2` | data constructor of an upsert term; the term renders no text, the upsert text is pinned by the query-upsert entries |

### Constants

- string constants and static read-only strings captured: 18

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
