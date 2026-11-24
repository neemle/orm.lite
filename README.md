<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="src/PackageAssets/logo-dark.svg">
    <img src="src/PackageAssets/logo.svg" alt="orm.lite" width="480" height="120">
  </picture>
</p>

# Neemle.OrmLite

A small, attribute-driven ORM for .NET on PostgreSQL or SQLite. You decorate plain classes; a source generator writes the
table mappings, the entity methods and the schema migrations at build time. The runtime that the generated code
calls ships in the same package, with soft delete, a row history that records who changed what, and typed queries
written as lambdas.

Inspired by [TypeORM](https://typeorm.io/), with an attribute-driven .NET API and build-time code generation.

Status: pre-release, API unstable. The emitted code shape, the runtime type names and the namespaces may change in
any 0.x release.

Native AOT: the runtime, the SQL kit and both provider libraries are built with `IsAotCompatible`, so the AOT and trim
analyzers run on them and a warning fails the build; the source generator and the core packer are build-time code and
are not. CI publishes one small NativeAOT consumer (a `Where()` over closure unaries) and runs it on .NET 10 and
.NET 11; beyond that, AOT compatibility is declared by the build and not demonstrated.

## Install

Three packages, one version. The provider packages depend on the core package at exactly the same version, so a
provider reference brings the core, the generator and the build assets with it.

| Package | What it is | Install |
| --- | --- | --- |
| `Neemle.OrmLite` | the provider-neutral runtime, the standard SQL grammar kit and the source generator | pulled in by either provider |
| `Neemle.OrmLite.Sqlite` | the SQLite provider (Microsoft.Data.Sqlite) | `dotnet add package Neemle.OrmLite.Sqlite` |
| `Neemle.OrmLite.Postgres` | the PostgreSQL provider (Npgsql) | `dotnet add package Neemle.OrmLite.Postgres` |

Referencing a provider package is enough: the build emits the provider's registration into your project, so
`OrmLiteOptions { Provider = "sqlite" }` (or `"postgres"`) resolves with no further code. There is no default provider; a
provider that is not referenced is a start-up error that names the package to add.

The packages are not on a public feed yet. Build them into the local feed folder `artifacts/` (which the repository's
`nuget.config` already names) before adding them:

    dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1

The packages target net10.0, and every consumer CI builds is net10.0 (a plain reference, central package management, a
lock file, `PrivateAssets=all`); a later .NET version is expected to work, and CI runs the tests and the NativeAOT smoke
consumer on both .NET 10 and .NET 11 (see "Build, test, pack, demo"). The source
generator is a net10.0 analyzer, so the compiler that loads it must run on .NET 10 or later (the `dotnet` CLI of the
.NET 10 SDK, or an IDE whose compiler host is .NET 10); a compiler hosted on .NET Framework, as in older Visual Studio
builds, cannot load it. The `Neemle.OrmLite` package depends on Neemle.CSharpComposer, which NuGet resolves; the
package holds no copy of it.

The PostgreSQL provider needs PostgreSQL 16 or newer: the JSON comparisons write `pg_input_is_valid`, which the server
has from 16, and the index reconcile writes `CREATE OR REPLACE TRIGGER`, from 14. `OrmLite.OpenAsync` reads the server
version when the provider starts and refuses an older server with a `NotSupportedException` that names the version it
found and the minimum, the way the SQLite engine minimum is checked. A data source you build yourself and hand to the
library without `OpenAsync` is not checked.

## Quickstart: three minutes, nothing to set up

You have a class and you want rows in and out of a database. You do not want to run a server, write SQL or learn a
schema tool. Everything below was run as written against the packed packages, on SQLite.

1. Make a project and add the package. Until the packages are on a public feed, point NuGet at the folder that
   `dotnet pack` filled (see Install):

       dotnet new console -n Notes && cd Notes
       dotnet nuget add source /path/to/orm.lite/artifacts --name orm-lite-local
       dotnet add package Neemle.OrmLite.Sqlite --prerelease

2. Describe a table by decorating a class (`Note.cs`):

       using Neemle.OrmLite;

       namespace Notes;

       [Table("notes")]
       public sealed class Note
       {
           [Key] public Guid Id { get; set; }
           public string Title { get; set; } = "";
           public int Stars { get; set; }
       }

3. Open the database, create the schema, use it (`Program.cs`):

       using Neemle.OrmLite;
       using Neemle.OrmLite.Generated;
       using Notes;

       await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = "notes.db" });
       await db.MigrateAsync();

       await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "hello", Stars = 5 });
       var best = await NoteOrm.Query(db).Where(n => n.Stars >= 3).ToListAsync();

   `dotnet run` creates `notes.db`, creates the table, inserts the row and reads it back. There is no SQL anywhere
   and no connection class to construct: the provider is a setting, the repository `NoteOrm` is generated from the
   class, and `db.MigrateAsync()` applies the generated baseline and your own `[Migration]` classes, which the build
   registered for the application's assembly (see Migrations).

A query is a builder: `Where`, `Take`, `Skip`, `OrderBy`, `ForUpdate`, `IncludingDeleted` and the other builder methods
change the query and return it, while terminals (`ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, `UpdateAsync` and
the other `Compile*`, `Render*` and `*Async` calls) never change it, so one query can feed several terminals; a query is
not thread-safe. `FirstOrDefaultAsync` reads at most the smaller of one and `Take` rows, so after `Take(0)` it returns
null.

The provider and its credentials are configuration, not code, and reading them needs no extra package. One call,
`OrmLiteOptions.Load()`, reads the `OrmLite` block of `appsettings.json` (next to the program, else in the current
folder, and optional) and then lets `OrmLite__*` environment variables override it setting by setting (the demo does
exactly that):

    { "OrmLite": { "Provider": "sqlite", "Path": "notes.db" } }

    await using var db = await OrmLite.OpenAsync(OrmLiteOptions.Load());

The same settings come from other places through dependency-free members of `OrmLiteOptions`:
`FromEnvironment()` (only the environment), `FromJsonFile(path, section, optional)` (a settings file other than
`appsettings.json`), `FromConnectionString("postgres", "Host=...;Database=...")`, and `FromSettings(pairs)`, the
binder the others use, which takes any sequence of `key, value` pairs whose keys read `OrmLite:Host` or `OrmLite__Host`.
An application that already has an `IConfiguration` passes it as is, and the core package gains no dependency:

    var options = OrmLiteOptions.FromSettings(configuration.AsEnumerable());

A key of the block that is not a setting, a port that is not a number and a missing provider are start-up errors that
name what is wrong; nothing is ignored silently.

## Switch to PostgreSQL

Reference the PostgreSQL package as well and change the block; no code changes:

    dotnet add package Neemle.OrmLite.Postgres --prerelease

    { "OrmLite": { "Provider": "postgres", "Host": "localhost", "Port": 5432, "Username": "postgres", "Database": "mydb" } }

`Provider` names the database and has no default: leaving it out is a start-up error that lists the installed
providers, and naming one whose package is not referenced is a start-up error that names the package to add.
`ConnectionString` is accepted by both providers for settings the discrete fields do not cover; fields a provider
does not use (`Path` for PostgreSQL, `Host` for SQLite) are refused by name, not ignored.

## The demo

[`demo/`](demo/) is this walk-through as a runnable program, a few small files, with its own
[README](demo/README.md). It references the packages from `artifacts/`, never a project, exactly as a consumer
does:

    dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1
    dotnet run --project demo                       # SQLite: creates a throwaway database file, removes it afterwards

and on PostgreSQL by changing the one `OrmLite` block (here through environment variables, which override
`demo/appsettings.json`):

    docker compose -f demo/compose.yaml up -d --wait
    OrmLite__Provider=postgres OrmLite__Host=localhost OrmLite__Port=55432 OrmLite__Username=postgres OrmLite__Database=postgres dotnet run --project demo
    docker compose -f demo/compose.yaml down -v     # when done: stops the container and removes its volume

Both end with the line `demo ok`; CI requires it on both. Beyond the quickstart the demo walks a changed schema
(columns, rename, foreign keys, indexes), units of work, indexes declared with `[Index]` (direction, collation,
expression, predicate), database-side aggregates and grouped reports with `HAVING`, a selection nested four tables
deep, JSON, a view over a join, cascading deletes, a ledger of exact decimals and a report of the statements it ran
through the diagnostics sink. [demo/README.md](demo/README.md) says which file shows what.

## Logging every statement, or only the slow ones

`OrmLiteOptions.Diagnostics` routes the SQL the provider serialised, its elapsed time, rows affected, connection and
transaction ids, and the transaction begin, commit and rollback events to a sink you assign: every statement, only
those over a threshold, or a sample. Parameter values are off by default and redacted when on: text that looks
like a secret, or that names one (a `password`, `token`, `api_key`, `api key`, `pin` or `Authorization` label in a JSON
document, a header line, a query string or an `a=b` pair, also when written with JSON escapes), is replaced whole,
whether it arrives as a string or as another value converted to text. A short unlabelled secret cannot be seen. Off by
default, and with no sink nothing is timed or allocated.

    options.Diagnostics.Sink = mySink;
    options.Diagnostics.SlowThreshold = TimeSpan.FromMilliseconds(200);

## Paging and ordering: which terminals take them

`Take`, `Skip` and the `OrderBy` family (`OrderBy`, `ThenBy`, `OrderByPredicate`, the JSON-key orders) shape the
rows a query reads. What a terminal does with them depends on what it is:

- Reads that return rows (`ToListAsync`, `ProjectAsync`, `ProjectionAsync`, `FirstOrDefaultAsync`) honour all three.
- `ScalarAsync` reads the column of the first row of the order: it honours `OrderBy`/`ThenBy` and `Skip`, and a
  `Take(n)` counts as at most one row, so `Take(0)` reads no row and gives the default value.
- Writes (`UpdateAsync`, `DeleteAsync`, `UpdateReturningAsync`, `DeleteReturningAsync`, and the hidden `PurgeAsync()`)
  refuse a query that carries `Take`, `Skip` or any order, with an `InvalidOperationException` that names the
  operation and every such clause. Nothing is executed and no row changes, on a `[SoftDelete]` entity too. A write
  affects every row that matches `Where`, and PostgreSQL has no `LIMIT` on `UPDATE` or `DELETE`, so a page of rows
  cannot be written without naming them.
- Aggregates (`CountAsync`, `AnyAsync`, `SumAsync`, `AggregatesAsync` and its `Compile`/`Render` forms) refuse `Take`
  and `Skip` the same way. An order alone is allowed and changes nothing.
- `GroupBy(...)` refuses a `Take`, `Skip` or order already set on the query, and a clause added to the query after
  `GroupBy` is refused when the grouping is compiled or run. Put them on the grouping, whose own `Take`, `Skip`,
  `OrderBy` and `OrderByKey` work.
- A row lock (`ForUpdate`, `ForNoKeyUpdate`) is taken by the reads that return rows (`ToListAsync`, `ProjectAsync`,
  `ProjectionAsync`, `FirstOrDefaultAsync`). Every other terminal refuses a query that carries one, with an
  `InvalidOperationException` that names the operation and the lock: `CountAsync`, `AnyAsync`, `SumAsync`,
  `AggregatesAsync` and its `Compile`/`Render` forms, `ScalarAsync` and its `Compile`/`Render` forms, `GroupBy(...)`,
  and the writes. They return no row to lock or lock the rows they change by themselves, so the lock was dropped
  without a word before. Read the rows with the lock in a unit of work, then count, sum or write by their keys.
- Every plain order term (`OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `OrderByPredicate`,
  `ThenByPredicate`) says `NULLS LAST`, ascending and descending, on both providers, so `OrderBy(x => x.Due).Take(n)`
  reads the same rows on SQLite and on PostgreSQL. Before, SQLite put NULLs first in both directions and PostgreSQL
  last when ascending and first when descending. The grouped orders already said the same. There is no plain form for
  NULLs first. A PostgreSQL index serves such an order when its term states the same placement
  (`Nulls = NullsPlacement.Last` on the `[Index]`; a descending term is `DESC NULLS LAST`), a capability SQLite
  refuses by name (`IndexNullsOrdering`).

To delete or update the first N rows, select their keys first and write by those keys:

    var ids = await NoteOrm.Query(db).Where(n => n.Stars < 3).OrderBy(n => n.Title).Take(100)
        .ProjectAsync([n => n.Id], r => r.GetGuid(0));
    await NoteOrm.Query(db).Where(n => ids.Contains(n.Id)).DeleteAsync();

or filter the write with `WhereIn` over a subquery that carries the order and the `Take`, which deletes exactly the
first N rows of that order:

    await NoteOrm.Query(db)
        .WhereIn<Note>(n => n.Id, sub => sub.Where(n => n.Stars < 3).OrderBy(n => n.Title).Take(100), n => n.Id)
        .DeleteAsync();

A negative `Take` or `Skip` is an `ArgumentOutOfRangeException` at the call, on the query and on a grouping alike:
PostgreSQL rejects `LIMIT -1` and SQLite reads it as no limit, so the same query would mean two things. Zero is allowed.

`ScalarAsync<TCol>` converts the value it reads the way the row mapper does, so the same call gives the same answer on
SQLite and on PostgreSQL. It reads int, long, short, byte and the other integral types (an integer that does not fit is
an `OverflowException`), float, double, decimal (the scale is kept), bool (a stored 0 or 1), Guid, DateTime (a stored
`Z` timestamp is read as UTC), DateOnly, an enum (the exact member name), `Jsonb` and string, each also as a nullable
type. It refuses a lossy or unknown conversion with an `InvalidCastException` that names the target and the type the
query returned and never the stored value: a fractional number into an integer, a double into a decimal, a number
into a string or an enum, an integer other than 0 and 1 into a bool, text that is not in the form the library writes,
and a type such as `DateTimeOffset`. A query that matches no row and a stored NULL both give `default(TCol)`; read a
nullable type to get `null`, and the two cannot be told apart. `ScalarAsync<object?>` returns the raw provider value,
which is provider-dependent by design: an integer column is a `long` on SQLite and an `int` on PostgreSQL.

## Capabilities per provider

Every provider declares, for each capability, `Native` (the database does it), `Emulated` (the provider does it with
the same or stronger meaning) or `None`. A declaration or call that needs a capability the provider does not have is
refused by name before any statement runs, and at build time when the project's providers are known (`ORMLITE015`,
`ORMLITE016`). This table is the content of the two manifests the packages ship
(`src/Postgres/assets/ormlite.capabilities.postgres.txt` and
`src/Sqlite/assets/ormlite.capabilities.sqlite.txt`), which a test compares with the providers'
declarations; at run time `db.Provider.Capabilities.Manifest()` prints the active provider's.

A manifest also states `MaxIdentifierBytes` when the provider keeps only so many bytes of a name. PostgreSQL keeps 63
and cuts a longer name without a word, so two long names could become one and `CREATE INDEX IF NOT EXISTS` would skip
the second. A migration that creates a table, column, constraint, index or view with a longer name (counted in UTF-8
bytes) is refused by name with `IdentifierTooLongException`, and the generator reports an entity's table, view, column,
index or join table name over the limit as `ORMLITE034`. SQLite states no limit.

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

Grouping (`Query<T>.GroupBy`, `Having`, `AggregateMax`, `AggregateAvg`) needs no capability beyond `AggregateFilter` for
`CountWhere`: both databases run `GROUP BY`, `HAVING` and the aggregate functions natively. The result types are what
differ. An average of a number column is cast to a `double` on both (`numeric` on PostgreSQL, `REAL` on SQLite); over a
decimal column `Avg` is refused, and `AvgDecimal(column, scale)` gives an exact `decimal` rounded half away from zero to the
scale you name. Sum, min and max of a decimal column are exact on both providers (`ExactDecimal`), also in `Having` and in
an ordering. Every grouped ordering term is `NULLS LAST`. The values of `Having` and the filters of `CountWhere` bind
into `BoundParameters` of the query the grouping came from, which is one list for every statement that query and its
groupings render, so a `Where` added afterwards takes the next slot. `AsView` is the opposite: it gives back the aliases
and values its columns allocate, so defining a view twice from one query gives two equal definitions.

## Migrations

The generator writes a baseline migration for your entities and one for their indexes; your own changes are classes
marked `[Migration]` that write only typed operations, so the same migration runs on either provider:

    [Migration("0100_welcome_note", Reversibility.Reversible)]
    public sealed class WelcomeNote : Migration
    {
        public override void Up(MigrationBuilder b) => b.InsertData("notes", ["id", "title", "stars"], [Id, "welcome", 0]);
        public override void Down(MigrationBuilder b) => b.DeleteData("notes", "id", Id);
    }

A row can also be given as an object, `b.InsertData(NoteOrm.Descriptor, NoteOrm.Values, new Note { ... })`; the object has the shape of the entity today, so
such a migration must come after the ones that add its columns.

`InsertData` and `DeleteData` write their values as quoted, invariant literals (a Guid, a date or time and an enum name
become single-quoted text, and a number keeps its invariant spelling whatever the thread culture is); an empty
`InsertData`, a `null` in `DeleteData`, a non-finite number, a value of a type with no literal form, and a string or
character that holds a NUL character or a lone surrogate is refused when the migration is built. On PostgreSQL a string
that holds a backslash is written as an escape string (`E'a\\b'`), so its meaning does not depend on
`standard_conforming_strings`; on SQLite a backslash is an ordinary character and nothing changes.

On SQLite a decimal value in `InsertData`, `DeleteData` or a view definition is written as quoted text (`'1.10'`), so
it keeps its scale and every digit the way a bound parameter does; on PostgreSQL it stays a bare numeric literal.

A null array given to `InsertData` or `DeleteData` is an `ArgumentNullException`, and negative zero is written as `0`.
Two limits of the targets are accepted, not worked around: SQLite stores an integer in 64 bits, so a `ulong` above
`long.MaxValue` is read back as a REAL (store it as text or a decimal); PostgreSQL keeps a moment to the microsecond,
so ticks beyond the sixth fractional digit are rounded when the row is stored.

`db.MigrateAsync()` applies every version not yet recorded in `schema_migrations`, in ordinal order, one
transaction each, for every assembly of the process that holds `[Table]` or `[Migration]` classes and references the
package: the build registers each assembly's generated contributions at start-up, with no reflection, so nothing is
named in code. `db.MigrateAsync(contributions)` applies an explicit list instead (a test, or an assembly the process has
not loaded yet), and `MigrationRunner.RunAsync(dataSource, provider, contributions)` is what both call;
`RevertAsync` runs a migration's declared or derived `Down` and refuses a migration declared `Irreversible`. A
migration whose operations are all reversible gets its
`Down` derived. Raw statement text exists only as the provider-scoped `MigrationBuilder.Raw(providerId, text)`
escape hatch.
Because the baseline is one recorded version, a table for an entity added after the first run is not created by
re-running: write a `[Migration]` with `CreateTable` for it.

`MigrationRunner.RunAsync` refuses a list that carries one version twice, applied or not, by name, before it touches
the database and before any provider refusal. On PostgreSQL it takes the advisory lock before it creates the history
table, so replicas migrating an empty database together queue on the lock, and it renders and validates every pending
migration before the first one begins, so a render or capability refusal in a later migration leaves the earlier ones
unapplied, as on SQLite; a SQL error raised while a migration runs still leaves the earlier ones committed.

## Limitations, stated plainly

- A `decimal` is exact on both providers: `numeric(p, s)` on PostgreSQL, and on SQLite decimal text under an exact
  collation with a fit check (`ExactDecimal` is `Emulated`). A decimal wider than 28 digits is refused by name on
  SQLite, and a decimal read back keeps the scale it was written with on SQLite, where PostgreSQL returns the
  declared scale; the values are equal and only their text differs.
- The row history of `[SoftDelete]` tables and JSON documents are `Emulated` on SQLite (see below) and differ from
  PostgreSQL in the ways listed there: a writer that is not the library cannot update a tracked table, and the state of
  a version is built from stored values.
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
- `Contains` over a column binds one parameter per value, so a list that would take a statement past the bind limit of
  the provider (32766 on SQLite, 65535 on PostgreSQL, counting the values already bound by earlier terms) is refused
  with a `NotSupportedException` that names the count and the limit; split the list into
  batches. A string that is asked to
  contain a string column (`"abc".Contains(x.Code)`) is refused too: it is a substring test, not an `IN` list.
  `StartsWith`, `EndsWith` and `Contains` over a column with a null value throw `ArgumentNullException`, as the C# call
  does, instead of matching every row; the `(value, ignoreCase, culture)` overloads ignore case when `ignoreCase` is
  true.
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

## How it works

At build time the generator reads the classes and records marked `[Table]` and `[Migration]` and emits,
through Neemle.CSharpComposer,
into the consuming project one `<Entity>Orm` class per table (a typed `Map`, an `EntityDescriptor`, insert, update, get,
delete, typed `Query`; the statements are node trees the core builds from the descriptor, never text in the generated
source), a `<AssemblyName>Schema` holding the descriptors, and an `<AssemblyName>Migrations` class whose `Contributions`
list the generated baseline, indexes, index reconciliation and history triggers and your hand-written migrations; the
core runner applies every pending one (through `db.MigrateAsync()`, which collects what each assembly registered, or
through `MigrationRunner.RunAsync(dataSource, provider, contributions)`) in version order,
one transaction each, under the provider's migration lock. Hand-written
migrations are classes with `Up` and `Down` that must say whether they are reversible. Mistakes in the declarations
(an unmapped property type, a `[SoftDelete]` entity without a tenant or a key, an entity with more than one key, a
property the generated code cannot assign or read, an entity type it cannot construct or, for a static one, generate, a
navigation whose foreign key is missing or of another type than the key it points at (`ORMLITE026`), two managed
entities on one table (`ORMLITE027`), a repeated or empty migration version (`ORMLITE028`, `ORMLITE029`), a `[Migration]` class the generated code cannot
build (`ORMLITE030`), an irreversible migration with a `Down`) are build errors with `ORMLITEnnn` ids, not runtime surprises. A property is a
column when it is a public instance property with an accessible get and a set or init accessor, so init-only and
`required` properties are supported and a private setter is refused (`ORMLITE023`); indexers and static classes are
not columns or entities (`ORMLITE024` names a static, abstract or not constructible entity). The `OrmLiteProviders`
build property names the
providers a project targets; with a capability manifest for each (an `ormlite.capabilities.<provider>.txt` additional
file) the generator refuses a declaration a provider cannot serve (`ORMLITE015`) and a call of a member marked
`[RequiresCapability]` or of a migration member that needs a capability (foreign key and view operations, decimal and
jsonb columns, nulls ordering, collation and `LowerOf` index terms, the `LiveOnlyConversionDdl`, `ForeignKeyActionDdl`
and `SoftDeleteColumnDdl` helpers) (`ORMLITE016`); `ORMLITE017` compares a migration's declared reversibility with its `Up`.
A migration body is written with the typed
`MigrationBuilder` (`CreateTable`, `AddColumn`, `CreateIndex`, `InsertData`, `DeleteData` and the rest); each
operation is recorded as a node, classified as reversible, lossy or unknown, and rendered by the provider. A
migration whose operations are all reversible gets its `Down` derived from `Up` by `Migration.Down`. `MigrationBuilder.Raw(providerId, text)`
is the one escape hatch for a statement the builder has no method for: it names the provider the text is written
for, it is refused on any other provider, and it has no derived inverse (a migration that holds it writes its own
`Down` or is declared `Irreversible`). You own that statement's dialect and safety. A column type or default is written
with `ColumnType` and `Defaults`, never as text, and none of the builders' convenience methods takes statement text
other than `Raw`. The node-level door is `Add(Node)`: it accepts a `RawSql` node, and a `RawSql` with no provider is
text that no provider owns, so write one only on purpose.

On SQLite the runner has no advisory lock to take: each version runs in one IMMEDIATE transaction behind the
single-writer gate, checks the history again inside it, and is validated before any of its statements runs, so an
operation the provider cannot run (a decimal wider than 28 digits, a session lock) is refused by
name and changes nothing. The operations `ALTER TABLE` cannot express (adding, dropping or converting a foreign key,
a column with an expression default or a primary key) are planned against the live schema as a table rebuild: foreign
key enforcement is switched off outside the transaction, the table is copied into a new one, and its indexes,
triggers and views are put back (views first, then the triggers: one defined on a view, one that names a view and one
on another table that names the rebuilt table are dropped before the views and recreated after them), then
`foreign_key_check` runs before the commit. `ADD COLUMN IF NOT EXISTS`,
`DROP COLUMN IF EXISTS` and the marker comments of the live-only index and soft-delete conversions are likewise
emulated from the catalog, and so is the generated index reconciliation: an index is created where its table and columns
exist, skipped where they do not, and a live-only index whose columns already carry a total unique index is refused by
name. On PostgreSQL an index is looked up through its own table, and an index name that another relation of the same
schema already holds is refused by name, because index names are schema-global there; table, index and column names with
quotes, percent signs, whitespace or dollar tags are quoted and escaped in every generated block, and a table lookup
accepts only an ordinary or partitioned table.
`MigrationRunner.RevertAsync` runs a migration's declared or derived `Down` and forgets
its version on either provider; a migration declared `Irreversible` is refused by name before anything runs, and the
generated baseline (it would drop every managed table with its rows) and the generated index reconciliation are
declared `Irreversible`. A declaration that is not a named member of `Reversibility` is refused the same way, and
`MigrationRunner.RunAsync` refuses it, a blank version and a default entry before any statement.
`MigrationOps.Audit(contribution, provider)` compares a hand-built entry's declaration with its operations (a
`Reversible` entry that holds an operation with no inverse and no `Down`, an `Irreversible` entry whose every operation
can be inverted); the runner does not refuse on a finding, so call it from a startup check or a test of your own.

Recovery plan of the three generated `Irreversible` entries (the migrations are applied by `MigrateAsync`, which a
deployment already authorises; none of them records the state it replaced):

- **Baseline (`0000_...`)** creates the managed tables and join tables with `CREATE TABLE IF NOT EXISTS`, so it
  touches no existing row. Its inverse would drop every managed table with its rows, which is why it is not
  reversible. Recovery: restore the database from a backup; on an empty database running the migration again creates
  the tables.
- **Index reconciliation (`9900_orm_index_reconcile_...`)** repairs index definitions only: it drops an index whose
  definition drifted and creates it again, and touches no table data. Recovery: migrate again (the version changes when
  the set of declared indexes changes) or recreate the index by hand from its declaration.
- **Row history (`9900_orm_history_reconcile_...`)** installs `row_history`, the capture function and its triggers; no
  table data is touched and rows already captured are kept. Recovery: migrate again, or drop the triggers and the
  function and migrate again.

The two version names of `9900_` carry a hash of what the assembly declares: the index reconcile the set of declared
indexes (table, index name, uniqueness, live column, columns), the row history the tables it covers and their key,
soft, tenant and unversioned columns, plus the actor setting name when it is not the default `ormlite.actor` (the
capture function names it, so a changed setting installs the function again). The hash is computed by the core from
those declarations alone, so the names are
the same whichever providers the process loads. An earlier build hashed the statements the PostgreSQL provider rendered whenever
that provider was loaded, so a PostgreSQL database created by that build carries the other names;
`MigrateAsync` there finds both versions unrecorded, runs them once and records the new names beside the old rows, and
the next run does nothing. Both installs are written to be run again (`CREATE TABLE IF NOT EXISTS`, `CREATE OR REPLACE`
for the function and the triggers, and an index is only dropped when its stored definition drifted from the
declaration), so the one repeated run changes nothing that is already right. SQLite already used the declaration hash.

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
against a server over thousands of seeded strings. What stays different, each pinned by a test: a number comparison is
an IEEE double, so integers differing past 2^53 compare equal; a hexadecimal float spelling and NaN read as no number;
text order is SQLite's byte order (PostgreSQL's under a `C` collation, not under others); a document written outside
the library is stored as written (the check still refuses malformed text) though every read treats it as canonical;
`orm_json_object` takes its JSON type from the SQLite storage class, so a boolean it is given becomes a number; a double
parameter compared to a text member keeps SQLite's spelling (`1.0`, not `1`), while booleans and decimals are bound as
PostgreSQL prints them; a statement that uses the functions cannot run on a bare connection to the same file, which
has none of them; and the refusals of malformed input are SQLite errors where PostgreSQL's are server errors, both
`DbException`s.

At run time the library holds the attributes, the query layer that turns lambdas into SQL, the migration
infrastructure and the connection gate. The gate stamps the acting user onto every connection it hands out, which
is what lets the history triggers attribute a change to a person, and clears it on a lease made with no actor, so a
command run again outside a `DbActor` scope is never attributed to the previous caller; it refuses connection strings
(`No Reset On Close`, `Multiplexing`) that would let a pooled connection keep the previous caller's stamp or send a
statement to another connection than the stamped one. Soft-deleted rows are hidden from queries until you ask for
them or restore them, and a restore brings back the rows that went into the trash in the same transaction.

The tenant scope of a row's history is one rule. The history reads (`RowHistoryRead`), the revert (`RowHistoryRevert`),
the restore (`SoftRestore`) and the trash list filter on the entity's tenant column, including an entity whose tenant
column is also its key; a wrong tenant finds nothing and changes nothing. A `Platform` entity takes no tenant and
ignores one. A call that gives no tenant for a tenant entity, and an entity that names no tenant column, are refused
with `InvalidOperationException`, so a missing scope never widens a statement to every tenant.

### Row history on SQLite

`RowHistoryTriggers` is declared `Emulated` on SQLite: the history of a row, the trash, a revert and a restore of a batch
deleted together give the same results on both providers (one suite runs on SQLite always and on PostgreSQL when
`ORMLITE_TEST_POSTGRES` names a server), and the capture is done by triggers, as on PostgreSQL. What differs is how the
trigger learns who acted and in which transaction, and a few properties follow from that:

- **The capture is a trigger plus three connection functions.** `RowHistoryDdl.Install` creates `row_history` and, per
  tracked table, an `AFTER UPDATE` and an `AFTER DELETE` trigger named `orm_history_<table>_u` and `_d`, and records the
  specification they were built from in the `orm_object_markers` table (`history:<table>`). The update trigger writes
  one history row with the old values of the versioned columns that changed, as a JSON object, and the operation
  (`update`, `soft_delete` or `restore`); the delete trigger removes the history of the row, as on PostgreSQL. Re-running the install replaces
  triggers whose specification or column set differs and leaves the others alone. The trigger names are
  fixed by the table, so a trigger of your own that merely starts with `orm_history_` is never taken for one of them and
  is put back by a rebuild; a column name with a `:` or `,` is stored escaped (marker version 2). A native `ADD COLUMN`, `RENAME COLUMN`
  or `DROP COLUMN` and a table rebuild regenerate the triggers around the change; a rename of a column the specification
  is built on (the key, the soft-delete or the tenant column) is followed, and a drop of one is refused by name.
- **The functions are registered on every connection the library rents.** The trigger calls `orm_actor()`, `orm_tx()` and
  `orm_tx_at()`, which the library registers each time it opens a connection (a pool does not keep them) and removes when
  it closes it, and registers through the engine API with `SQLITE_INNOCUOUS`, so they work when `trusted_schema` is off
  and, by design, under native AOT (not yet exercised in CI). Every other function the library writes into a schema object is registered the same way
  (`orm_decimal_fits` in the CHECK of a decimal column, `orm_json_*` and `orm_epoch_millis`, `orm_ilike`, and the
  `orm_decimal_sum` and `orm_decimal_average` aggregates), so a decimal column or a `[View]` also works with
  `trusted_schema` off. The acting user is the one the caller entered with `DbActor`; with none the change is recorded
  with the author `unknown`, as on PostgreSQL. A statement outside a unit of work is its own transaction: it gets its own
  identity and time, and every statement of a unit of work shares one, which is what a batch restore matches on.
- **A writer that is not the library cannot update a tracked table: that is a property, not a defect.** A second connection
  (the `sqlite3` shell, another tool, another process) has none of the functions, so an `UPDATE` of a tracked table fails
  with `no such function: orm_actor` and changes nothing; reading, inserting and deleting still work, and a delete removes the
  history of the row. An ops tool that must update such a table has to define the same three functions.
- **`recursive_triggers` is on** in the connection string the provider builds, so an `INSERT OR REPLACE` over a tracked row
  fires the delete trigger and purges the history of the replaced row instead of handing it to the new one.
- **Limits of the JSON image.** A BLOB value cannot be recorded and aborts the update of a tracked table that holds one,
  and the state of a version is built from the stored values: a boolean is `0` or `1`, a guid or timestamp is text and a
  JSON column is its stored text, where PostgreSQL's `jsonb` row image has `true`, `false` and nested documents.
- **`ConstraintColumns.ForAsync` finds indexes on SQLite.** SQLite gives a table constraint no name of its own, so a name
  is resolved as an index (a `CREATE UNIQUE INDEX`, or the automatic index of a constraint) and a constraint name that
  is not an index name has no columns.

## Build properties

- `OrmLiteActorSetting`: the PostgreSQL custom setting that carries the acting user, `prefix.name` in lower case
  (default `ormlite.actor`); every assembly that references the runtime registers it at start-up. A value that breaks
  the rule fails the build (`ORMLITE032`). The initializer and the provider registration are written in C#, so a Visual
  Basic or F# project gets neither (a set `OrmLiteActorSetting` there gives the warning `ORMLITE033`)
- `OrmLiteDefaultTenant`: the tenant property a soft-delete entity uses when it does not declare one (default
  `TenantId`)
- `OrmLiteProviders`: the provider ids the project targets, separated by `;` (for example `postgres;sqlite`); it
  switches on the capability diagnostics `ORMLITE015` and `ORMLITE016`. It defaults to the provider packages the
  project references, whose capability manifests the build passes to the generator; set it to override
- `OrmLiteRegisterProviders`: `false` stops the build from emitting the provider registration (register the
  provider yourself with its factory's `Register()`)

`ExcludeAssets="analyzers"` on the package reference gives a runtime-only consumer: it still receives the build
assets and so the actor setting, and the generators do not run in it.

## Repository layout

- `src/Generator`: the net10.0 analyzer (entity and migration generators and the capability analyzer; they render through Neemle.CSharpComposer)
- `src/Runtime`: the runtime assembly (attributes, migrations, query layer, history, soft delete)
- `src/Sql`: the standard SQL grammar kit the providers share
- `src/Postgres`, `src/Sqlite`: the providers; each packs its assembly, its capability
  manifest and the props file that declares it to the build
- `src/Core`: the core packer: analyzer in `analyzers/dotnet/cs`, runtime and Sql kit in `lib/net10.0`, build
  assets in `buildTransitive`
- `src/Packaging.props`: the metadata the three packages share
- `src/Directory.Build.props`, `tests/Directory.Build.props`: a project is named by its folder, so `src/Postgres` builds
  the assembly, root namespace and package id `Neemle.OrmLite.Postgres`; the prefix is written once, as `OrmLiteRoot` in
  the root `Directory.Build.props`. A file's namespace is the root namespace plus its folder path, and the build fails
  (IDE0130 as an error, set in the `.editorconfig` files under `src/` and `tests/`) when one is not. The exceptions are
  folders with their own `.editorconfig` that gives the reason: the public diagnostics types of
  `src/Runtime/Diagnostics` stay in `Neemle.OrmLite`, and `tests/Tests/Corpus/Support` keeps
  `Neemle.OrmLite.Tests.Corpus` because a golden embeds it
- `src/PackageAssets`: the SVG logo and icon originals, plus the 128 x 128 `icon.png` used by all three packages;
  [the asset guide](src/PackageAssets/README.md) covers the light and dark variants and how to regenerate the PNG
- `CHANGELOG.md`: what changed in each version of the packages
- `src/PackageReadme/README.md`: the short consumer README that is packed into all three packages, in place of this
  file; it links only to public URLs of this repository
- `tests/Tests`: the library's own tests (the generator driven over fixtures, the query layer over an
  in-process SQLite; the PostgreSQL half needs a server, see "Build, test, pack, demo")
- `tests/compose.yaml`, `tests/skip-allowlist.txt`, `tests/skip-witness.txt`: the throwaway PostgreSQL that is the local
  twin of the CI test job's service, and the two lists the skip gate reads
- `tests/PackConsumer`: the throwaway consumers that `scripts/PackCheck.cs` builds from the packed package
- `scripts/`: thirteen file-based C# tools (`dotnet run scripts/X.cs`), no shell: `PackCheck.cs` (package entries, the
  consumer matrix, the demo guard), `ReadmeCheck.cs` (the packed README is the consumer README, and no file names
  documentation outside this repository), `PackVersions.cs` (every packed assembly carries the package version),
  `NupkgTool.cs` (lists or shows a package's entries), `CorpusCapture.cs` (the rendered-text corpus), `SkipGate.cs`
  (fails the test job on a skipped test that is not allow-listed), `WorkflowShape.cs` (fails when the workflow drops
  the compose step, the variable or the gate), `StepShape.cs` (fails when a test step loses its one-thread cap or
  the zone step and its gate go, or the main test step its 15-minute timeout and its console log),
  `MatrixShape.cs` (fails when the test and AOT jobs lose a .NET 10 or .NET 11 leg or run a leg on the wrong runtime),
  `ComposeCoherence.cs` (the PostgreSQL image major and the two host ports are written in the compose files and
  must be the same in `ci.yml` and the READMEs), `StageShape.cs` (fails when a CI job loses its stage, its `needs` or a
  gate step of its stage), `RetentionShape.cs` (fails when the retention workflow widens its triggers, its permissions
  or its steps, or gets a gate on its deletion) and `RunRetention.cs` (the retention script itself)
- `.github/workflows/ci.yml`: four ordered stages. Stage 1 runs the guards and a strict build; stage 2 builds and tests
  against the PostgreSQL of `tests/compose.yaml`, with the skip gate, once on .NET 10 and once on .NET 11; stage 3 packs
  with the consumer matrix and the guards that need the packages, and runs the NativeAOT smoke consumer on both
  runtimes; stage 4 runs the demo on SQLite (no service) and on PostgreSQL (the server of `demo/compose.yaml`)
- `.github/workflows/retention.yml`: after every finished `ci` run, it keeps the newest 20 workflow runs and
  deletes the older ones with their artifacts, with no manual step (see "Build, test, pack, demo")
- there is no documentation folder: the user documentation is this README and the commented `demo/` (see
  "Documentation" below)
- `demo/`: the demo, a consumer of the packages, with an `appsettings.json` block that names the provider and a
  `compose.yaml` for a throwaway PostgreSQL

## Build, test, pack, demo

Plain dotnet commands, from a fresh clone, with the .NET 10 SDK or a later one:

    dotnet build Neemle.OrmLite.slnx -c Release       # the library and its tests
    dotnet test Neemle.OrmLite.slnx -c Release        # the tests; no database needed
    dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1   # the three packages
    dotnet run --project demo                          # the demo on SQLite, a consumer of the packages in ./artifacts

The demo needs no database server. To run it on PostgreSQL, start the throwaway one and override the provider block
(see "The demo" above), the settings being the `OrmLite` section:

    docker compose -f demo/compose.yaml up -d --wait   # a throwaway PostgreSQL on 127.0.0.1:55432
    docker compose -f demo/compose.yaml down -v        # stop and remove it, and any volume it created, when done

The demo restores the packages from `./artifacts` into `artifacts/.demo-packages`; the demo's build notices that the
packed files changed and drops its restored copy of the three packages before restoring, so packing the same version
again and running the demo always uses the new build.

The behavioural suites (queries, migrations, JSON documents, row history, trash, restore and revert) run on SQLite always
and on PostgreSQL when `ORMLITE_TEST_POSTGRES` holds the connection string of a server that may create databases (for
example `Host=127.0.0.1;Username=postgres`); each run creates and drops a database of its own.

The whole suite is memory-hungry: measured in a container, one test process running every test with
`xUnit.MaxParallelThreads=1` peaked at about 4.2 GiB on a quiet machine and at 4.98 GiB under a 5 GiB cap, and the
default parallel run was killed at 3 GiB and at 6 GiB. A run that ends with "Test host process crashed" and no failing
test has been seen under host load and may be the same kill (check `docker inspect --format '{{.State.OOMKilled}}'` and
exit code 137; it was not confirmed for those runs), so give the test container well over 5 GiB, pass
`-- xUnit.MaxParallelThreads=1` (`scripts/StepShape.cs` fails when a CI test step drops it), or run the suite in
`--filter` chunks. `--blame-crash` (CI passes it) records which test was running when the host died. The cause of the
size is not proven. The strongest suspect is that seven generator test helpers rebuilt about 150 metadata references and
a compilation on every call; they now share one list (`GeneratorReferences`). The consolidated run measures the peak
memory (`docker stats` of the test container, and the TRX) with that change; until then it is a suspect, not a finding.

A plain local `dotnet test` stays green with the PostgreSQL classes skipped; only the skip gate turns a skip into a
failure. The CI test job runs twice, once per .NET runtime (`10.0.x` and `11.0.x`, each leg on its own runner and its
own throwaway server, `fail-fast: false`): the runtime can change what a test observes (the conversion of a `double` to
a `decimal` differs between them). The test project targets net10.0, so on the .NET 11 leg the test host rolls forward
to the installed .NET 11 runtime (`DOTNET_ROLL_FORWARD=LatestMajor`; the .NET 10 leg names the default `Minor`), and
`TestHostRuntimeTests` fails a leg whose host runs on another major than `ORMLITE_EXPECT_DOTNET` names. The shipped
libraries stay net10.0. The `aot-smoke` job publishes the NativeAOT consumer on both runtimes too (its target framework
follows the leg: `-p:AotSmokeTfm=net10.0` or `net11.0`) and compares the runtime the binary prints with the leg;
`scripts/MatrixShape.cs` fails when either job loses a leg or this wiring. Each leg uploads its own
`test-results-<version>` artifact. The CI test job starts `tests/compose.yaml` (the `neemle/pg.lite:19` image) with
`docker compose up --wait`, sets `ORMLITE_TEST_POSTGRES` for the test step, runs `dotnet test` with the TRX logger and
one test thread (`xUnit.MaxParallelThreads=1`; the default parallel run was killed for memory in a capped container),
and then runs `dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx tests/skip-allowlist.txt
--require-postgres tests/skip-witness.txt`. The gate fails on a skipped test that is not on
`tests/skip-allowlist.txt` with a reason (the tests skipped on PostgreSQL by design), on a listed test that did not
skip, on a malformed or inconsistent TRX, and on a class of `tests/skip-witness.txt` (hand-written, server-only
classes) that is absent or has no passed result. `dotnet run scripts/WorkflowShape.cs -- check` fails when an edit
removes the compose step, the variable, the logger or the gate step from the test job, or loosens `tests/compose.yaml`
or `demo/compose.yaml` (the image tag, the exec-form health check, the loopback ports, the tmpfs); both tools have a
`self-test` that CI runs first (in stage 1, before any test runs). The same run locally, against the throwaway server of
`tests/compose.yaml` (image `ORMLITE_TEST_PG_IMAGE`, default `neemle/pg.lite:19`; host port `ORMLITE_TEST_PG_PORT`,
default 55433; the file's header lists the other variables for the image's own port, role and data path; the demo's
own throwaway server is `demo/compose.yaml`, on the same image):

    docker compose -f tests/compose.yaml up -d --wait
    ORMLITE_TEST_POSTGRES='Host=127.0.0.1;Port=55433;Username=postgres' dotnet test tests/Tests -c Release \
      --logger "trx;LogFileName=tests.trx" --results-directory TestResults -- xUnit.MaxParallelThreads=1
    dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx tests/skip-allowlist.txt \
      --require-postgres tests/skip-witness.txt
    docker compose -f tests/compose.yaml down

The test job then runs the data-literal tests once more under a non-UTC time zone (`TZ=Pacific/Auckland`, the
filter `FullyQualifiedName~DataLiteral&FullyQualifiedName!~Postgres`, its own TRX `TestResults/zone.trx`) and gates that
TRX with `tests/zone-allowlist.txt` (empty: nothing may skip) and `tests/zone-witness.txt` (the classes that must
pass). A `DateTimeKind.Local` moment is converted to UTC when it is written as a literal or bound, and the default
UTC runner cannot tell that conversion from none; `scripts/StepShape.cs` fails when the step or its gate goes.
The same run locally (needs the zone database; without it the zone falls back to UTC and `LocalZoneInForceTests`
fails):

    TZ=Pacific/Auckland dotnet test tests/Tests -c Release \
      --filter "FullyQualifiedName~DataLiteral&FullyQualifiedName!~Postgres"

The core is also run on a third provider that is not PostgreSQL and not SQLite. `tests/Ports.Tests`
references the Runtime and Sql projects only (no provider package, no driver) and holds a fake provider built from the
public ports: a serializer, a capability set and an actor stamp (it supplies no migration planner, so the core's
lock-free default runs), over an ADO.NET source that returns canned rows. Its tests run the migration runner and a
query on it. The test job runs the project in a step of
its own with its own TRX (`TestResults/ports.trx`, a TRX logger overwrites a file of the same name, so each test project
has its own step), and gates it with `tests/ports-allowlist.txt` (empty) and `tests/ports-witness.txt`. The
neutrality ratchet, `CoreNeutralityTests` of the main test project, scans the Runtime and Sql sources for provider
types, ids and SQL text and fails on any that `tests/Tests/Ports/core-coupling-allowlist.txt` does not
record; that list is empty and a line is never added to it. The same test freezes the project references. The
same run locally:

    dotnet test tests/Ports.Tests -c Release

CI (`.github/workflows/ci.yml`) then runs `dotnet run scripts/PackCheck.cs -- package artifacts`
(the entries, versions and dependencies of the three packages and the throwaway-consumer matrix: default reference,
runtime-only, analyzer-only, default tenant, actor setting, downstream project, central package management,
`PrivateAssets=all`, a lock file, and consumers of only the Sqlite
package, only the Postgres package, both and neither, which also prove the registration, the default provider list,
the opt-out and an application over a data library), `-- demo-guard` (the demo must reference the packages at the packed
version, never a project or an assembly, and carry no `nuget.config` or `global.json` of its own) and
`-- demo-guard-self-test` (the guard's own bypass cases). Every consumer restores Neemle.CSharpComposer, and the
provider consumers Npgsql and Microsoft.Data.Sqlite, from the package cache (`ORMLITE_PACK_PACKAGES`,
`NUGET_PACKAGES` or the user cache), so restore the solution first. CI also packs again in two demo jobs and runs the
demo on SQLite (no service) and against a PostgreSQL started from `demo/compose.yaml` with `docker compose up --wait`,
requiring the `demo ok` line from each, the soft delete, restore and history chapter included (see demo/README.md).
CI packs into the workspace only; nothing is published.

The CI jobs run in four ordered stages, each a set of jobs that `needs` the one before: `1 static` (every guard and
self-test that needs no server, then `dotnet build -c Release -warnaserror`), `2 test` (one leg per .NET runtime),
`3 package` and `3 aot-smoke` (the packages with the consumer matrix, and the NativeAOT smoke on both runtimes), and
`4 demo-sqlite` and `4 demo-postgres`. A stage starts only when the one before it passed, so a broken gate or build
stops the run early, and the jobs of one stage run in parallel; the test legs build again rather than take the build
of stage 1 (one extra build in the run, and no artifact hand-off). `scripts/StageShape.cs` fails when a job loses its
stage name, its `needs` or a gate step of its stage, when a job-level `if:` or a `continue-on-error` could skip a stage,
or when an uploaded artifact has no `retention-days`; the test results are kept for 7 days.

`.github/workflows/retention.yml` cleans the history of workflow runs by itself, with no switch to turn on and no step
for a person. It runs after every finished `ci` run (on any branch or pull request), once a day as a backstop, and on
request, and on each of them it deletes: it keeps the newest 20 workflow runs of the repository, counted over all
workflows together (the `keep` input of a manual run, never fewer than 5), and deletes every older completed run, which
deletes its logs and artifacts. The runs of the retention workflow itself, one for each finished `ci` run, are left out
of that count so that they cannot push a `ci` run out of the 20, and are trimmed on their own to the newest 5. It never
deletes the run it is executing in nor a run that is queued, in progress or waiting, and a run that is already gone
counts as deleted. The only workflow that uploads artifacts is `ci` (the test results, kept for 7 days), so deleting
the runs leaves no artifact behind. A manual run has a `dry_run` input, false by default, to only list what would be
deleted. GitHub starts a workflow that listens to another workflow only from the default branch, so the automatic runs
start once the file is on `main`. Its token has `actions: write` and `contents: read` only, overlapping runs queue one
after the other, and `scripts/RunRetention.cs` calls the GitHub API for the list of runs and for the deletion of one
run, nothing else, so no branch, tag, release or file is touched. `scripts/RunRetention.cs -- self-test` checks the
selection (fewer runs than the limit, exactly the limit, more, unsorted input, the current run, runs that are not
completed, a limit below 5, ties in the creation time, the one count over all workflows, and the retention runs left
out of it and trimmed to 5) and the dry-run setting, and `scripts/RetentionShape.cs` pins the workflow: its three
triggers, the `false` default of `dry_run`, that nothing gates the deletion, and the count that leaves the retention
runs out; both run in stage 1.

`dotnet run scripts/PackCheck.cs -- package artifacts` also runs `dotnet run scripts/ReadmeCheck.cs -- all artifacts`:
each of the three packages must carry `src/PackageReadme/README.md` as its `README.md`, that file must have the
Status, Install, Quickstart, Providers and Documentation sections, name the three packages, link only to public URLs
of this repository, give no local-feed step (`dotnet pack`, `nuget add source`) and no line over 120 characters, and
every file that git tracks, this script included, must be free of references to documentation that is held outside
the repository (the patterns are assembled at the top of the script, so that the script does not spell them). Every
tracked file is scanned whatever its name, except a binary one (a NUL byte in its first 8000 bytes); an ignored or
untracked file is never scanned, so an agent's local files cannot turn the check red. The same run refuses a
`src/Packaging.props` that does not pack exactly the consumer README (item, `PackagePath`, `Pack`, `PackageReadmeFile`),
a `scripts/PackCheck.cs` that does not call its guard function with `all` and test the exit code, and a
`.github/workflows/ci.yml` whose `package` job does not run `ReadmeCheck.cs -- self-test`, `ReadmeCheck.cs -- source`
and `PackCheck.cs -- package` in that order. `ReadmeCheck.cs -- self-test` feeds each rule a violating and a clean
input, the tree rules each in a throwaway git repository started without the caller's `GIT_*` variables and git
configuration; CI runs it first, then `-- source` as a step of its own, which catches a removed call in
`PackCheck.cs`, then `PackCheck.cs -- package artifacts`.

## Documentation

The user documentation lives in this repository and nowhere else: this README (configuration, capabilities per
provider, migrations, limitations, build properties) and the runnable, commented [`demo/`](demo/) with its own
[README](demo/README.md). The README that the NuGet packages carry is the short page `src/PackageReadme/README.md`
(install, quickstart, providers, status), which links to this README and to the demo by their public GitHub URLs. A
change to what the library does is described here, in the same change; nothing a reader of a package or of this
repository needs is kept elsewhere.

## License

MIT, see `LICENSE`; third-party components are listed in `THIRD-PARTY-NOTICES.md`.
