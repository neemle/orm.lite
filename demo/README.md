# Neemle.OrmLite demo

You have a C# class and you want rows going in and out of a database. You add one package, put attributes on the
class, and it works: no server to start, no SQL to write, no connection plumbing. This folder is that, end to end,
in a few small files. It runs on SQLite with nothing to set up, and on PostgreSQL by changing one block of
configuration.

## Run it

You need the .NET 10 SDK or a later one. From the repository root:

    dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1
    dotnet run --project demo

The first command builds the three packages into `artifacts/`, a local feed that the root `nuget.config` names. The
demo restores them exactly as a consumer would (it references packages, never the library projects), creates a
throwaway SQLite database in a temporary folder, runs, removes the folder, and its last line is `demo ok`.

## The same demo on PostgreSQL

The demo reads one block of `appsettings.json` with `OrmLiteOptions.Load()`, which needs no package beyond the library:

    { "OrmLite": { "Provider": "sqlite" } }

Change it to name PostgreSQL and its credentials. Nothing else changes, because the demo code never names a
database. Environment variables override the file, so you can do it without editing anything:

    docker compose -f demo/compose.yaml up -d --wait
    OrmLite__Provider=postgres OrmLite__Host=localhost OrmLite__Port=55432 OrmLite__Username=postgres OrmLite__Database=postgres dotnet run --project demo
    docker compose -f demo/compose.yaml down -v

`compose.yaml` starts a throwaway PostgreSQL (the `neemle/pg.lite:19` image, which has no shell: its health check is the
server's own `postgres --healthcheck`) with trust authentication, published on 127.0.0.1:55432 only. To use a
database you already have, put its settings in the block (or in `OrmLite__ConnectionString`).

Both providers print `demo ok`. A provider that is missing from the project is a start-up error that names the package
to add, and there is no default provider.

## How to read the demo

The demo prints eighteen numbered lines and then `demo ok`. Each number is a chapter. The source files are commented
for a reader who has not seen the library: every file starts with what it shows and which library features it uses, and
the code carries comments on the steps that are not obvious. This section says in what order to read them.

### Order of reading

1. **`Program.cs`**: the whole program in one screen, and the roadmap of chapters in its header comment. Read it first.
2. **`Note.cs`**: the smallest entity (attributes, indexes, one explicit migration). Chapters 1 to 4 use it.
3. **`Trash.cs`**: soft delete, restore and the change history (chapters 5 to 7).
4. **`Tracker/Entities.cs`**: the four entities of the tracker and how they relate, with a diagram.
5. **`Tracker/Migrations.cs`**: how the schema changed, version by version, and why the versions run in that order.
6. **`Tracker/Chapters.cs`**: chapters 8 to 16, each with a heading comment (purpose, what to notice, what it prints,
   what differs between SQLite and PostgreSQL). **`Tracker/Views.cs`** goes with chapter 14.
7. **`Ledger.cs`** (chapter 17: exact decimals and a transfer in a unit of work) and **`SlowQueryReport.cs`** (chapter 18:
   seeing the SQL the library ran). **`ScratchDatabase.cs`** is housekeeping: it makes and removes the temporary
   SQLite file.

Each chapter of `Tracker/Chapters.cs` opens with a heading comment that says what it shows and what differs per
provider; read it beside the chapter's code.

### What to run

    dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1     # once, from the repository root
    dotnet run --project demo                                         # SQLite: no setup

Read the printed lines against the table below. A chapter that prints its lines and still goes wrong shows up only at the
end: the last line is `demo FAILED` and the exit code is 1, because every chapter returns whether its read-back values
were the expected ones. On PostgreSQL (see above) the lines are the same except the provider id in line 1, the user id
in line 7 (random on every run), and the statement counts and timings in line 18.

The demo expects a **fresh database** on every run. It writes rows that later chapters rely on, and some chapters change
what earlier ones check: chapter 8 expects the first project's JSON settings to be `{}` (chapter 13 rewrites them) and
chapter 14 expects the view as migration `0280` defined it (it replaces it). The SQLite run makes a new file each time
and the compose file makes a new database each time you bring it up.

### What each chapter proves

- **Chapter 1** (`Program.cs`): one call opens the provider named in the settings and creates or upgrades the schema.
  See [Migrations](../docs/migrations.md).
- **Chapter 2** (`Note.cs`, `Program.cs`): an explicit migration runs between the generated ones, and inserts an object,
  not SQL.
- **Chapter 3, 4** (`Program.cs`): generated insert, update, get and delete, and a lambda query run by the database. See
  the [SQLite quickstart](../docs/quickstart.md).
- **Chapter 5 to 7** (`Trash.cs`): a deleted row is kept and hidden, restore brings it back, and the history says who
  changed it. See "Soft delete, restore and history" below.
- **Chapter 8** (`Tracker/Migrations.cs`, `Tracker/Chapters.cs`): a schema changed by hand-written migrations on tables
  that hold rows keeps its data and reaches the expected shape, read from the database catalog.
- **Chapter 9** (`Tracker/Chapters.cs`): several statements commit together or not at all: a refused change and an
  exception both leave nothing behind.
- **Chapter 10** (`Tracker/Chapters.cs`, `Note.cs`): a descending composite index is written per provider from one typed
  call; attributes create indexes with a direction; a unique index refuses a duplicate.
- **Chapter 11** (`Tracker/Chapters.cs`): count, sum, min, distinct count and filtered counts come from one statement.
- **Chapter 12** (`Tracker/Chapters.cs`): a selection three levels deep over foreign keys is one statement of nested
  `EXISTS`.
- **Chapter 13** (`Tracker/Chapters.cs`): a JSON column can be filtered, ordered and changed in place on both providers.
- **Chapter 14** (`Tracker/Views.cs`, `Tracker/Chapters.cs`): a view is defined with a typed query, created by a
  migration, read like a table, replaceable, hash-checked and read-only.
- **Chapter 15** (`Tracker/Chapters.cs`): deleting a team removes its projects, items and comments through three
  cascading foreign keys.
- **Chapter 16** (`Tracker/Chapters.cs`): `GROUP BY`, `HAVING` and ordering by an aggregate, with typed results,
  including over a filter that is two nested subqueries.
- **Chapter 17** (`Ledger.cs`): exact decimals, a row lock and a transfer that is all or nothing.
- **Chapter 18** (`SlowQueryReport.cs`): every statement the library ran reaches a sink you supply, with its text and
  duration, and no parameter values.

### Words the comments use

- **Entity**: a class with `[Table]`, mapped to a table. The generator writes a repository for it, `<Entity>Orm`.
- **Managed**: `[Table("x")]` (the default) means the generated baseline migration creates the table.
  `Managed = false` means the table is created by a migration you write; the tracker entities are declared that way
  because their schema has a history (`Tracker/Entities.cs` says why).
- **Migration**: a version of the schema. The generator writes a baseline (`0000_...`) and the indexes (`0001_...`); your
  `[Migration]` classes are the others (`0100_...`, `0200_...` to `0280_...`); versions run in ordinal order, once each.
- **Node tree**: how the library holds a statement before a provider writes it as SQL. Nothing in the demo is SQL text.
- **Capability**: whether a provider does something natively (`Native`), with the same meaning by other means
  (`Emulated`) or not at all (`None`, which is refused by name). The comments name the capability where a chapter
  differs between SQLite and PostgreSQL; see the [provider capability table](../docs/providers.md).
- **Unit of work** (`UnitOfWork`): one connection and one transaction; `CommitAsync()` keeps it, leaving without it
  undoes it.

### The settings file, key by key

`appsettings.json` cannot hold comments, so this is its commentary. The file is the `OrmLite` block that
`OrmLiteOptions.Load()` reads; `OrmLite__*` environment variables override it setting by setting.

| Key | Value in the demo | Meaning |
| --- | --- | --- |
| `OrmLite:Provider` | `"sqlite"` | which database to use; there is no default, and the provider's package must be referenced. `"postgres"` is the other value |
| `OrmLite:Diagnostics:StatementLevel` | `"Debug"` | report every statement to the sink at this level; `"None"` (the default) reports none. Chapter 18 shows what arrives |
| `OrmLite:Diagnostics:SlowThresholdMilliseconds` | `5` | a statement that took at least this many milliseconds is flagged `Slow` (and reported at the slow level, `Warning` by default); `0` switches it off |

There is no `Path`, so on SQLite `ScratchDatabase.cs` makes a temporary file. The other settings the block accepts are
`ConnectionString`, `Host`, `Port`, `Database`, `Username`, `Password`, `Path`, `Pooling` and `BusyTimeoutSeconds`; the
`Diagnostics` block takes further settings (see [SQL diagnostics](../docs/diagnostics.md)). The sink itself is code, not a setting, which is why `Program.cs` assigns `options.Diagnostics.Sink`
before opening the database: asking for reports with no sink is a start-up error. `compose.yaml` carries its own
comments.

## The whole idea

1. Add the packages. A provider package brings the core with it; this demo references both providers so the block
   above can name either.

       dotnet add package Neemle.OrmLite.Sqlite --prerelease

   The packages are not on a public feed yet: `dotnet pack` builds them into `artifacts/`.

2. Describe the table by decorating a class (`Note.cs`).

       [Table("notes")]
       [Index("title", Unique = true)]
       public sealed class Note
       {
           [Key] public Guid Id { get; set; }
           public string Title { get; set; } = "";
           public string Body { get; set; } = "";
           public int Stars { get; set; }
       }

3. Open the database and create or upgrade the schema (`Program.cs`).

       await using var db = await OrmLite.OpenAsync(OrmLiteOptions.Load());
       await db.MigrateAsync();

4. Use it. Every table gets a generated `NoteOrm` class.

       await NoteOrm.InsertAsync(db, note);
       var liked = await NoteOrm.Query(db).Where(n => n.Stars >= 3).OrderBy(n => n.Title).ToListAsync();
       await NoteOrm.DeleteAsync(db, note.Id);

## What the demo shows

| Step | What happens |
| --- | --- |
| 1 | the provider named in the block is opened and the generated migrations create the table and its index |
| 2 | `WelcomeNote`, an explicit reversible `[Migration]`, seeds a row with the typed `InsertData` of an object (and removes it with `DeleteData`): no SQL |
| 3 | insert, update, read back and a lambda query |
| 4 | delete, then the row is gone |
| 5 to 7 | soft delete, restore and the change history of a `Draft` |
| 8 | a tracker schema (`Tracker/`) that was built and changed by nine migrations: new columns with defaults, a rename, foreign keys added to tables that hold rows, indexes added and dropped, read back from the catalog |
| 9 | a unit of work of several statements: commit, a refused commit that rolls back, a failure partway |
| 10 | indexes: `[Index]` on `Note` (a descending property index and a composite one, read back from the catalog with their direction), a composite index with a descending column through `IndexTerm` as each provider writes it, and a unique composite index |
| 11 | count, sum, min and filtered counts in one statement over the whole result |
| 12 | a selection across four tables through nested `Db.Exists`, one statement |
| 13 | JSON: key filters, array membership, ordering by a key, in-place merge and increment |
| 14 | views: a `[View]` entity defined by a typed query, created by a migration, read like a table, replaced and dropped (`Tracker/Views.cs`) |
| 15 | `ON DELETE CASCADE` through three foreign keys |
| 16 | a report in database-side groups: per status with `HAVING` and ordering by the sum, per project, and per status for the teams of a region through two nested subqueries |
| 17 | a ledger of decimal accounts: a transfer in one unit of work, an overdraft refused (`Ledger.cs`) |
| 18 | the diagnostics sink (`SlowQueryReport.cs`) prints how many statements reached it and the slowest three |

The `Diagnostics` block of `appsettings.json` asks for every statement and flags those over 5 ms; chapter 18 shows what
the sink received.

The run ends with `demo ok` only when every read-back value is what was written: the seeded row, the updated stars,
the query result, the delete, the trash count, the restore and the history entries with their actor,
and when the temporary database folder is gone again.

The tracker chapters (8 to 16) and the migration history behind them are in `Tracker/`: `Migrations.cs` holds the
versions and `Chapters.cs` the chapters. The library does not do materialized views or a type change yet.
`WelcomeNote` inserts its row as an object with `InsertData<T>(NoteOrm.Descriptor, NoteOrm.Values, note)`; the tracker
seed does the same.

## Soft delete, restore and history

`Draft` (`Trash.cs`) is a `[SoftDelete]` entity: deleting it moves the row to the trash, `IncludingDeleted()` still
sees it, `RestoreAsync` brings it back, and with `using (DbActor.EnterUser(userId))` around the work every update,
soft delete and restore is recorded in `row_history` with that user. `[SoftDelete]` needs the `RowHistoryTriggers`
capability, which PostgreSQL has natively and SQLite emulates (see [Row history on SQLite](../docs/row-history.md#row-history-on-sqlite)), so the chapter runs on either provider and prints the same lines. On SQLite the history is captured by
triggers that call functions the library registers on its own connections; a tool that updates the database file
through another connection cannot update the `drafts` table.

## Defaults you did not have to set

- the soft-delete tenant column is `TenantId` (override with the `OrmLiteDefaultTenant` build property, or per
  entity with `[SoftDelete(Tenant = "OwnerId")]`; `SoftDeleteAttribute.Platform` means no tenant at all)
- the soft-delete column is `DeletedAt` (`[SoftDelete("RemovedAt")]` names another property)
- the PostgreSQL setting that carries the acting user is `ormlite.actor` (override with the `OrmLiteActorSetting`
  build property, a lowercase `prefix.name`)
- table and column names are the snake_case of the class and property names
- the SQLite database is created if it does not exist, with foreign keys enforced and a 30 second busy timeout

## Things that are worth knowing before you copy this

**The SQLite file in the demo is disposable.** When the block names SQLite and no `Path` or `ConnectionString`,
`ScratchDatabase.cs` creates a temporary folder, points `Path` at a file in it, and removes the folder when the demo
ends. Name a `Path` and the file is yours and stays.

**`[Index]` takes property or column names; a term can carry a direction.** `[Index("tenant_id", "title desc", Unique = true,
LiveOnly = true)]` names a column and a property with a sort direction, while `Tenant = "OwnerId"` and `[SoftDelete("RemovedAt")]` name C#
properties. An `[Index]` on a property indexes that field. `LiveOnly` makes a unique index that only counts rows that
are not in the trash (`ORMLITE004` when it is not unique or the entity is not `[SoftDelete]`).

**An explicit `[Migration]` runs between the generated ones.** `db.MigrateAsync()` collects every migration,
generated and explicit, and applies the ones not yet recorded in `schema_migrations` in ordinal order of their
version string, one transaction each. The generated baseline is `0000_...` and the generated index migration
`0001_...`; the reconciliations that finish a run start with `9900_`, so `0100_welcome_note` runs after the tables
exist and before the reconciliations. Every `[Migration]` names `Reversibility.Reversible` or `Reversibility.Irreversible`
(`ORMLITE006` otherwise).

**The baseline is applied once.** A table for an entity added after a database has run the baseline is not created by
running again; write a `[Migration]` that creates it.

**`db.MigrateAsync()` finds the migrations by itself.** The build registers each assembly's generated migrations
when the assembly starts, so there is no class to name. To migrate an explicit list (a test, or an assembly the program
has not touched yet), pass it: `db.MigrateAsync(<YourAssemblyName>Migrations.Contributions)`, the generated class
in the `Neemle.OrmLite.Generated` namespace (letters and digits of the assembly name only).

**`[SoftDelete]` history records changes, not creation.** The triggers fire on update, soft delete and restore, so
an insert is not in the history. A change made outside `DbActor.EnterUser` is still recorded, with actor kind
`unknown` (`DbActor.EnterSystem` records kind `system`).

**The demo restores into `artifacts/.demo-packages`.** `Directory.Build.targets` compares the packed files with the
ones it restored last time and, when they differ, removes its restored copy of the three packages first, so packing the
same version again and running the demo uses the new build.
