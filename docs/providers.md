# Provider capabilities and limits

[Documentation](README.md)

## Capabilities per provider

Every provider declares, for each capability, `Native` (the database does it), `Emulated` (the provider does it with
the same or stronger meaning) or `None`. A declaration or call that needs a capability the provider does not have is
refused by name before any statement runs, and at build time when the project's providers are known (`ORMLITE015`,
`ORMLITE016`). This table is the content of the two manifests the packages ship
(`src/Postgres/assets/ormlite.capabilities.postgres.txt` and
`src/Sqlite/assets/ormlite.capabilities.sqlite.txt`), which a test compares with the providers'
declarations; at run time `db.Provider.Capabilities.Manifest()` prints the active provider's.

### Identifier length

A manifest also states `MaxIdentifierBytes` when the provider keeps only so many bytes of a name. PostgreSQL keeps 63
and cuts a longer name without a word, so two long names could become one and `CREATE INDEX IF NOT EXISTS` would skip
the second. A migration that creates a table, column, constraint, index or view with a longer name (counted in UTF-8
bytes) is refused by name with `IdentifierTooLongException`, and the generator reports an entity's table, view, column,
index or join table name over the limit as `ORMLITE034`. SQLite states no limit.

### Capability matrix

| Capability | What it covers | PostgreSQL | SQLite |
| --- | --- | --- | --- |
| `Upsert` | insert or update on a key conflict | Native | Native |
| `Returning` | read back the written row | Native | Native |
| `PartialIndex` | the unique-while-live index (`[Index(LiveOnly = true)]`) | Native | Native |
| `IndexNullsOrdering` | `nulls first` or `nulls last` in an `[Index]` term | Native | None, refused by name |
| `IndexCollation` | `collate` in an `[Index]` term | Native | Native (BINARY, NOCASE, RTRIM) |
| `IndexExpression` | `lower(column)` in an `[Index]` term | Native | Native |
| `AggregateFilter` | counting a filtered subset, also per group (`AggregateCountWhere`, `Grouping.CountWhere`) | Native | Native |
| `NullSafeEquality` | equality that treats two nulls as equal | Native | Native |
| `ArrayParameter` | a list bound as one parameter (`Contains`, `IN`) | Native | Emulated |
| `RowLock`, `KeyPreservingRowLock` | row locks inside a unit of work | Native | Emulated |
| `NamedLock` | named exclusive and shared locks | Native | Emulated |
| `IntervalArithmetic` | adding an interval to a time | Native | Emulated |
| `AlterConstraint` | add, drop or convert a foreign key | Native | Emulated |
| `ObjectMarker` | markers the library keeps on tables, indexes, views (`CreateView`, `ReplaceView`, `DropView`, `[View]`) and history triggers | Native | Emulated |
| `CatalogGuard` | `IF NOT EXISTS` style guards on schema changes | Native | Emulated |
| `SessionLock` | a lease held for a whole session | Native | None |
| `JsonDocument` | `Jsonb` properties and the `Db.Json*` markers | Native | Emulated |
| `RowHistoryTriggers` | the change history of a `[SoftDelete]` table | Native | Emulated |
| `ExactDecimal` | exact `decimal` columns (`[Numeric(p, s)]`): compare, order, index, sum | Native | Emulated |
| `WalCheckpoint` | `Maintenance.CheckpointWalAsync`, a WAL checkpoint in an explicit mode ([maintenance](maintenance.md#wal-checkpoint)) | None, refused by name | Native |
| `Vacuum` | `Maintenance.VacuumAsync` in `Plain` mode ([maintenance](maintenance.md#vacuum)) | Native | Native (checkpoint, then VACUUM) |
| `VacuumFull` | `VacuumMode.Full`, which rewrites the table | Native | None, refused by name |
| `VacuumTable` | a VACUUM of one table (`VacuumRequest.Table`) | Native | None, refused by name |
| `Analyze` | `Maintenance.AnalyzeAsync` of the database or one table ([maintenance](maintenance.md#analyze)) | Native | Native |

### Grouped query behavior

Grouping (`Query<T>.GroupBy`, `Having`, `AggregateMax`, `AggregateAvg`) needs no capability beyond `AggregateFilter` for
`CountWhere`: both databases run `GROUP BY`, `HAVING` and the aggregate functions natively. The result types are what
differ. An average of a number column is cast to a `double` on both (`numeric` on PostgreSQL, `REAL` on SQLite); over a
decimal column `Avg` is refused, and `AvgDecimal(column, scale)` gives an exact `decimal` rounded half away from zero to the
scale you name. Sum, min and max of a decimal column are exact on both providers (`ExactDecimal`), also in `Having` and in
an ordering. Every grouped ordering term is `NULLS LAST`. The values of `Having` and the filters of `CountWhere` bind
into `BoundParameters` of the query the grouping came from, which is one list for every statement that query and its
groupings render, so a `Where` added afterwards takes the next slot. `AsView` is the opposite: it gives back the aliases
and values its columns allocate, so defining a view twice from one query gives two equal definitions.

## Limitations, stated plainly

### Decimals and row history

- A `decimal` is exact on both providers: `numeric(p, s)` on PostgreSQL, and on SQLite decimal text under an exact
  collation with a fit check (`ExactDecimal` is `Emulated`). A decimal wider than 28 digits is refused by name on
  SQLite, and a decimal read back keeps the scale it was written with on SQLite, where PostgreSQL returns the
  declared scale; the values are equal and only their text differs.
- The row history of `[SoftDelete]` tables and JSON documents are `Emulated` on SQLite (see [Row history](row-history.md#row-history-on-sqlite) and [JSON on SQLite](#json-on-sqlite)) and differ from
  PostgreSQL in the ways listed there: a writer that is not the library cannot update a tracked table, and the state of
  a version is built from stored values.
### SQLite concurrency

- SQLite is one writer at a time. Writes and units of work take a gate keyed by the database file; a second writer
  waits for it and fails
  with an error once the busy timeout passes (30 seconds unless `BusyTimeoutSeconds` says otherwise), instead of
  waiting forever. The gate is keyed by the file the engine opens, so two spellings of one file share it: a relative
  and an absolute path, a symlink and its target, `|DataDirectory|` and the directory it stands for, a `file:` URI and
  its path, and, on a case-insensitive volume, names that differ only in case. The path is fixed when the data source
  is created: a relative name, a `|DataDirectory|` name and a relative `file:` URI are made absolute then, so a later
  change of working directory cannot move an open database; the key itself is computed once, at the first use of the
  data source, and kept. Hard links, bind mounts, network shares, Windows short and UNC names and Unicode
  normalisation variants that reach one file by two names are not recognised. The minimum engine
  version is checked when a connection opens.
- Several processes may write one SQLite file only through that busy timeout.
### Enums and null comparisons

- An enum column is stored as its member name, so a database ranks two of them alphabetically and not by value. A
  `Where` that compares an enum column with `<`, `<=`, `>` or `>=` (also through an `(int)` cast), and an `OrderBy`,
  `OrderByDescending`, `ThenBy` or `ThenByDescending` over an enum column, are refused with a `NotSupportedException`
  that names the column and its type; nothing is executed. Equality, inequality and `Contains` on an enum column work.
  `AggregateMin`, `AggregateMax`, a grouping's `Min` and `Max` and a view column's `Min` over an enum column, and
  `OrderByKey` on a grouping whose key is an enum column, are refused the same way (the answer or the order would be the
  alphabetical one). `GroupBy` on an enum column, and an order by an aggregate, still work: a group key is read as the
  enum value, a nullable enum key as `null` for the NULL group, and a stored name that is not a member raises an
  `OrmEnumValueException` that names the table, the column and the accepted members, as a row read does.
  To rank by value, keep an integer column beside the enum and order or compare that, or sort the rows in memory.
- `Where` follows SQL, not the C# lambda, where NULL is concerned. `x.Body != "a"` and `!(x.Rank == 4)` are `<>` and
  `NOT (=)`, which are not true for a row whose column is NULL, so that row is left out; the lambda would keep it.
  To keep the NULL rows, say so: `x.Body == null || x.Body != "a"`, or `!Db.NullSafeEquals(x.Body, "a")`.
  A relational comparison with a NULL value is refused, see the next bullet.
- A relational comparison (`<`, `<=`, `>`, `>=`) of a column with a null value (`nullable < n.Level` when the variable
  holds null, or a null literal) is refused with a `NotSupportedException` that names the column: SQL never makes it
  true, so it used to render a predicate that selected no row. `== null` and `!= null` are `IS NULL` and `IS NOT NULL`
  as before; test with those, or pass a value that is not null.
### Filtering and parameter limits

- `Contains` over a column binds one parameter per value, so a list that would take a statement past the bind limit of
  the provider (32766 on SQLite, 65535 on PostgreSQL, counting the values already bound by earlier terms) is refused
  with a `NotSupportedException` that names the count and the limit; split the list into
  batches. A string that is asked to
  contain a string column (`"abc".Contains(x.Code)`) is refused too: it is a substring test, not an `IN` list.
  `StartsWith`, `EndsWith` and `Contains` over a column with a null value throw `ArgumentNullException`, as the C# call
  does, instead of matching every row; the `(value, ignoreCase, culture)` overloads ignore case when `ignoreCase` is
  true.
### Cancellation, masking, and writes

- No `Query<T>`, `Grouping` or `OrmExec` method takes a `CancellationToken` yet; a running statement is cut short by the
  command timeout or by closing the connection. Adding the parameter to every terminal is one change of the public
  surface and is not done piecemeal.
- `Redact` and `RedactWhen` mask the column in every read that returns it row by row: the rows, `ProjectAsync`,
  `ProjectColumn` with `ProjectionAsync`, and `ScalarAsync`. An aggregate, a `GroupBy` key or a grouping's aggregate
  over a masked column is refused with a `NotSupportedException` that names the column, because it would hand back
  what the mask hides. A `Where` on a masked column still filters; the mask hides values, not the existence of rows.
- `SumAsync` returns a `long`, so it is refused with an `InvalidOperationException` for a `double`, `float` or `decimal`
  column (it used to round a sum of 0.4 to 0). Add the column with `AggregateSum` and read it from `AggregatesAsync`,
  which keeps the type.
- An upsert `UpsertSet.Increment` counts a NULL counter from zero (`COALESCE(column, 0) + n`), as `SetIncrement` does.
  `OnConflict.DoUpdate` with no `UpsertSet` is refused with an `InvalidOperationException` (it used to be written as
  `DO NOTHING`, and on a `[SoftDelete]` entity without the live-row guard); pass the sets, or use
  `OnConflict.DoNothing`.
- A `Where` comparison (`==`, `!=`, `<`, `<=`, `>`, `>=`) needs a column of the queried entity on at least one side.
  A comparison with none (a member of a captured object against a value, arithmetic over a column against a value, or
  two values) is refused with a `NotSupportedException` that names both operands; nothing is executed. Compare a
  column with a value, or work the value out first and pass the result. The same refusal applies to a column selector
  (`OrderBy`, `ThenBy`, `Redact`, an aggregate or a `Set` target) that is such a member and not a column of the row.

## JSON on SQLite

### Storage and functions

JSON documents (`Jsonb`, the `Db.Json*` and `Db.MovedField*` markers, `OrderByJsonKey*`, `SetJson*`) are declared
`Emulated` on SQLite (`JsonDocument=Emulated` in its capability manifest) and behave as on PostgreSQL's `jsonb`, which
one shared behavioural suite checks on both providers. A JSON column is `text` guarded by `CHECK (json_valid(column))`
(not a `jsonb()` blob, which the ADO reader returns as bytes and `json_valid` rejects by default). Every document the
library writes passes through `orm_json`, which rejects what `jsonb` rejects (a NUL escape, a lone surrogate, malformed
text) and stores the canonical form: keys ordered by length then bytes, the last duplicate kept, numbers and escapes
printed as PostgreSQL prints them, non-ASCII text unescaped. The operations are connection functions the provider
registers on every connection it opens (`orm_json_text`, `orm_json_has`, `orm_json_array_has`, `orm_json_concat`,
`orm_json_remove`, `orm_json_object`, `orm_json_int`, `orm_json_number`, `orm_json_double`, `orm_json_boolean`,
`orm_epoch_millis`), not SQLite's own `->>`, `json_patch` and `json_extract`, because those differ from `jsonb` in ways
a statement cannot paper over: `->>` returns the JSON type, so a number sorts before text (`9` before `10`, where text
order puts `10` first) and a key starting with `$` is read as a path and raises an error; `json_patch` removes a key set
to null and merges nested objects where `||` keeps the null and replaces the nested object; an integer past int64
read through `json_extract` or `->>` becomes a REAL. A key is always a member name, never a path. A number comparison
uses the same input grammar as PostgreSQL's `numeric` (and `double precision` for the moved-number order), checked
against a server over thousands of seeded strings.

### Differences from PostgreSQL

The shared tests cover the differences below.

- **Numbers:** Comparisons use IEEE double, so integers differing past 2^53 compare equal. Hexadecimal floats and NaN read as no number.
- **Text:** Text order is SQLite's byte order (PostgreSQL's under a `C` collation, not under others).
- **External writes:** A document written outside
the library is stored as written (the check still refuses malformed text) though every read treats it as canonical.
- **Storage types:** `orm_json_object` takes its JSON type from the SQLite storage class, so a boolean it is given becomes a number; a double
parameter compared to a text member keeps SQLite's spelling (`1.0`, not `1`), while booleans and decimals are bound as
PostgreSQL prints them.
- **Connection requirements:** A statement that uses the functions cannot run on a bare connection to the same file, which
has none of them; and the refusals of malformed input are SQLite errors where PostgreSQL's are server errors, both
`DbException`s.
