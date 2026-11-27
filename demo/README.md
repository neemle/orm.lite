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

The demo prints chapters 1 to 18, with the graph and foreign-key chapters (both labelled 16a) between 16 and 17,
and then `demo ok`.
Each number is a chapter. Source comments explain the library features and steps that are not obvious.
This section gives a reading order for someone who has not seen the library.

The separately selected [graph-save chapter](#save-an-explicit-graph) uses the same project to demonstrate atomic
related writes. Invoke it with `graph-save sqlite` or `graph-save postgres`; it is not part of the default numbered run.

### Order of reading

1. **`Program.cs`**: the whole program in one screen, and the roadmap of chapters in its header comment. Read it first.
2. **`Note.cs`**: the smallest entity (attributes, indexes, one explicit migration). Chapters 1 to 4 use it.
3. **`Trash.cs`**: soft delete, restore and the change history (chapters 5 to 7).
4. **`Tracker/Entities.cs`**: the four entities of the tracker and how they relate, with a diagram.
5. **`Tracker/Migrations.cs`**: how the schema changed, version by version, and why the versions run in that order.
6. **`Tracker/Chapters.cs`**: chapters 8 to 16, each with a heading comment (purpose, what to notice, what it prints,
   what differs between SQLite and PostgreSQL). **`Tracker/Views.cs`** goes with chapter 14.
7. **[Graph/GraphChapter.cs](Graph/GraphChapter.cs)**: chapter 16a loads an explicit graph; its three models and
   [fixture](Graph/GraphFixture.cs) show the declarations, seed data and cleanup.
8. **[ForeignKeys/ForeignKeyChapter.cs](ForeignKeys/ForeignKeyChapter.cs)**: chapter 16a uses an opted-in foreign key;
   its [parent](ForeignKeys/ForeignKeyParent.cs) and [child](ForeignKeys/ForeignKeyChild.cs) show the declarations.
9. **`Ledger.cs`** (chapter 17: exact decimals and a transfer in a unit of work) and **`SlowQueryReport.cs`** (chapter 18:
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
- **Chapter 16a** ([Graph/GraphChapter.cs](Graph/GraphChapter.cs)): requested Books, Tags and nested Editor for two
  paged authors, with exact membership, shared identity and an explicit row-budget refusal. See
  [Requested graphs](#requested-graphs-chapter-16a) below.
- **Chapter 16a** ([ForeignKeys/ForeignKeyChapter.cs](ForeignKeys/ForeignKeyChapter.cs)): a named generated FK,
  nullable updates, orphan insert/update refusal and physical parent deletion with `SetNull`. See
  [Generated foreign keys](#generated-foreign-keys-chapter-16a) below.
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

       dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0

   The command installs the public release. This repository’s demo uses packages packed into `artifacts/`.

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
| 16a | a finite typed graph over two paged authors: Books, Tags and Books → Editor, plus overflow without a partial returned list ([Graph/GraphChapter.cs](Graph/GraphChapter.cs)) |
| 16a | a fresh generated FK: exact catalog name/endpoints/action, nullable writes, orphan refusal and physical `SetNull` ([ForeignKeys/ForeignKeyChapter.cs](ForeignKeys/ForeignKeyChapter.cs)) |
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

## Requested graphs (chapter 16a)

### Run this chapter with JIT or NativeAOT

Use the same project and [GraphChapter](Graph/GraphChapter.cs) checks directly after packing the current packages
as shown in [Run it](#run-it):

```sh
dotnet run --project demo/OrmLiteDemo.csproj -c Release -- graph sqlite
export ORMLITE_GRAPH_POSTGRES='Host=localhost;Username=postgres;Database=postgres'
dotnet run --project demo/OrmLiteDemo.csproj -c Release -- graph postgres
```

The graph route creates and removes its own scratch SQLite folder or uniquely named `graph_chapter_*` PostgreSQL
database. Use only a disposable PostgreSQL server where the account may create and drop databases. Credentials are
never printed. The default invocation without `graph` runs the full numbered demo.

With the NativeAOT prerequisites installed, publish the same project for the machine that will run the binary:

```sh
dotnet publish demo/OrmLiteDemo.csproj -c Release -r linux-arm64 -p:PublishAot=true -o artifacts/graph-native
artifacts/graph-native/OrmLiteDemo graph sqlite
artifacts/graph-native/OrmLiteDemo graph postgres
```

For .NET 11 use its SDK and `-p:DemoTfm=net11.0`, with a separate output directory. The chapter prints the actual
runtime, its existing membership/page/identity/overflow checks and `graph chapter ok; scratch removed` on success.
JIT and native executions use the same chapter, models and assertions described below.

[GraphAuthor](Graph/GraphAuthor.cs), [GraphBook](Graph/GraphBook.cs) and [GraphTag](Graph/GraphTag.cs) declare
collection, reference and many-to-many navigations. The [fixture](Graph/GraphFixture.cs) seeds four authors,
three books and two tags, then removes its rows and links when the chapter finishes.

[GraphChapter](Graph/GraphChapter.cs) compares an ordinary `ToListAsync()` with
`Page(db).ToGraphListAsync(Request(20))`. Both use the same `Where`, `OrderBy`, `Skip(1)` and `Take(2)`:
the page contains Alba and Bruno, and related rows do not multiply or consume its two roots.
The generated paths are `GraphAuthorOrm.Paths.Books`, `GraphAuthorOrm.Paths.Tags` and
`GraphAuthorOrm.Paths.Books.Then(GraphBookOrm.Paths.Editor)`.

The chapter checks each author's exact books and tags. Bruno is the same object as the requested Editor references,
and both authors share one tag instance. A nullable Editor stays null; the unrequested `GraphBook.Author` stays untouched.
The ordinary query leaves both navigation collections empty.

Every request supplies `GraphLimits(GraphTraversalLimits, GraphWorkLimits)` with six positive bounds:

| Group | Bound | Value in this fixture |
| --- | --- | --- |
| Traversal | `MaxDepth` | 2 |
| Traversal | `MaxPaths` | 3 |
| Traversal | `MaxNodes` | 3 |
| Work | `MaxRoots` | 2 |
| Work | `MaxRelatedRows` | 20 |
| Work | `MaxStatements` | 8 |

Choose all six for your application's fan-out and work. A second call uses `Request(1)` to lower only the
related-row bound. It catches `GraphLimitExceededException` for `GraphBudget.RelatedRows` and verifies that no list
was returned. A completed graph is returned only after every requested path succeeds.

Graph reads use sequential statements and add no implicit transaction, snapshot guarantee or automatic retry.
With `On(unitOfWork)` or `On(connection, transaction)`, the caller retains commit, rollback and disposal ownership.
See the [graph guide](../docs/relations.md#5-load-an-explicit-graph) for bound accounting, visibility and failure types.

## Generated foreign keys (chapter 16a)

The [parent](ForeignKeys/ForeignKeyParent.cs) and [child](ForeignKeys/ForeignKeyChild.cs) are managed tables.
The child's nullable `Guid? ParentId` backs its `Parent` navigation, declared with
`[Reference(nameof(ParentId))]` and `[ForeignKey(ConstraintName, ReferentialAction.SetNull)]`.
The explicit constraint name is `fk_demo_child_parent`: `demo_fk_children.parent_id` references
`demo_fk_parents.id`. The [chapter](ForeignKeys/ForeignKeyChapter.cs) reads the real catalog and checks the single
constraint's exact name, endpoints and delete action on both providers.

Generated repositories insert a valid parent and child, update the child's `ParentId` to null and restore it, and read
the values back. Orphan inserts and updates must be refused. The parent has no `[SoftDelete]` annotation, so
`ForeignKeyParentOrm.DeleteAsync(db, parent.Id)` deletes it physically; the child survives with `ParentId` null.
The chapter then deletes its child row. Database `SetNull` applies to a physical delete; a soft-delete update
does not trigger an `ON DELETE` action or introduce an ORM navigation-save policy.

**Check enforcement as well as the catalog.** On SQLite, the chapter reads `PRAGMA foreign_keys` and requires `1`,
then exercises the orphan refusals and physical `SetNull`. Catalog or DDL presence alone does not establish
enforcement. The factory enables foreign keys when the connection string omits the setting; an explicit
`Foreign Keys=False` remains disabled and unenforced. The chapter expects enforcement and does not override that
choice. PostgreSQL enforces the installed constraint independently of this SQLite setting.

**Use a fresh database.** The opt-in adds the constraint to the generated baseline. An already recorded baseline
is not replayed when an attribute is added: existing databases need a reviewed explicit migration and validation
of their existing rows. The tracker's manually owned foreign keys and migrations remain unchanged, and a
loader-only `[Reference]` declaration adds no database constraint. See the
[relation guide](../docs/relations.md#3-choose-how-to-create-foreign-keys) and
[upgrade guidance](../docs/migrations.md#foreign-keys-fresh-baseline-and-existing-databases).

## Save an explicit graph

The [GraphSave guide](GraphSave/README.md) runs the same public cases and models with JIT or NativeAOT on
SQLite and PostgreSQL, using .NET 10 or 11. Pack the current checkout as shown in [Run it](#run-it), then select it:

```sh
dotnet run --project demo/OrmLiteDemo.csproj -c Release -- graph-save sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' \
  dotnet run --project demo/OrmLiteDemo.csproj -c Release -- graph-save postgres
```

[GraphSaveDemo](GraphSave/GraphSaveDemo.cs), selected by [Program.cs](Program.cs), creates a fresh scratch database
and runs these checks before removing it:

| Case | What it checks |
| --- | --- |
| [Default-off](GraphSave/GraphSaveDefaultCase.cs) | Populated navigations remain unwritten without an explicit rule. |
| [Insert](GraphSave/GraphSaveInsertCase.cs) | Reference, collection and one exact Guid-key link commit together; FK binding leaves input objects untouched. |
| [Repeat](GraphSave/GraphSaveRepeatCase.cs) | Explicit updates and Unchanged anchors preserve omitted siblings and links without duplicating the pair. |
| [Rollback](GraphSave/GraphSaveRollbackCase.cs) | Two successful writes precede a third-statement constraint conflict; rollback removes the new prefix and preserves the existing row. |
| [Refusal](GraphSave/GraphSaveRefusalCase.cs) | Wrong actions, missing anchors, enabled FK constraints, partial inputs and pre-cancellation have their expected outcomes. |

Exit zero and `graph-save chapter ok; scratch removed` require every case and scratch cleanup to succeed.
See the guide for [models and explicit intents](GraphSave/README.md#models-and-explicit-intents),
[NativeAOT/.NET 11 commands](GraphSave/README.md#run-with-jit-or-nativeaot) and
[outcome and ownership boundaries](GraphSave/README.md#outcome-and-ownership-boundaries).

## Replace an owned collection

[OrphanDemo](Orphans/OrphanDemo.cs), selected by [Program.cs](Program.cs), uses ordinary public APIs and a fresh
scratch database. The same [parent](Orphans/OrphanDemoParent.cs), [child](Orphans/OrphanDemoChild.cs),
[soft child](Orphans/OrphanDemoSoftChild.cs) and assertions are used for either provider and runtime mode.
Pack the current checkout as shown in [Run it](#run-it), then select this chapter:

```sh
dotnet run --project demo/OrmLiteDemo.csproj -c Release -p:DemoTfm=net10.0 -- orphans sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' \
  dotnet run --project demo/OrmLiteDemo.csproj -c Release -p:DemoTfm=net10.0 -- orphans postgres
```

`OrphanCollections.Relation<TParent,TChild,TParentKey,TChildKey>(navigation)` resolves a generated, typed owner-FK
binding. `BeginAsync(context, relation, parentKey, options, ct)` opens and owns one transaction, observes the complete
ordinary LIVE membership and returns an `OrphanReplacement<TChild,TChildKey>`. Its immutable `Observation` identifies
the original keys; `ApplyAsync(request, ct)` consumes that scope once. Supply five positive finite `OrphanLimits`
and explicitly select `Nullify`, physical `Delete`, or declared `SoftDelete`; the default `Disabled` mode grants no
authority and performs no database work. Nullify requires a nullable owner FK, and protected soft rows cannot be
physically purged by this API.

`OrphanReplacementRequest` carries desired keys and an immutable `OrphanPresentWrites<T>` builder: explicitly
`Insert` new desired children or `Update` observed ones, or pass `None`. Omitted original keys alone become removal
candidates. Present writes and exact candidates commit together; input objects, keys, FKs and collections remain
unchanged. Merely omitting a navigation in an ordinary graph save does not request removal.

| Case | What it checks |
| --- | --- |
| [Disabled](Orphans/OrphanDemoDisabled.cs) | No authority or implicit removal; explicit dormant writes and repeated apply are refused. |
| [Replacement](Orphans/OrphanDemoReplacement.cs) | Exact nullify and physical delete, duplicate collapse, unrelated owners and authoritative empty observation. |
| [Present writes](Orphans/OrphanDemoPresent.cs) | Insert, update and candidate removal commit together without mutating inputs. |
| [Soft delete](Orphans/OrphanDemoSoft.cs) | LIVE hides the retained row; native history records the actor and original owner. |
| [Refusals](Orphans/OrphanDemoRefusals.cs) | CallerJoined, unsupported mode/purge, unobserved membership and wrong owner are refused. |
| [Bounds](Orphans/OrphanDemoBounds.cs) | Observed, desired, candidate, write and cumulative statement bounds refuse atomically. |
| [Rollback](Orphans/OrphanDemoRollback.cs) | A later PK conflict rolls back a successful present-write prefix and preserves existing membership. |
| [Cancellation](Orphans/OrphanDemoCancellation.cs) | The caller token survives cancellation; abandoned scopes roll back and release their resources. |

Exit zero and `orphans chapter ok; scratch removed` require all assertions and cleanup to succeed. The API owns its
connection and transaction; `CallerJoined` is refused. Dispose scopes with `await using`. A rollback acknowledgement
means `RolledBack`; disposal alone does not prove rollback. A lost commit response means `Unknown`, while cleanup
failure after an acknowledged commit reports `Committed` with `CommittedResult`. Inspect `OrphanException.Error`
and `Outcome`; its message and `ToString()` are safe, while a privileged `InnerException` can contain provider data.
Do not blindly retry an unknown outcome.

For NativeAOT, publish the same source for the machine that will execute it:

```sh
dotnet publish demo/OrmLiteDemo.csproj -c Release -p:DemoTfm=net10.0 -r linux-arm64 \
  -p:PublishAot=true -o artifacts/orphans-native-net10
artifacts/orphans-native-net10/OrmLiteDemo orphans sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' \
  artifacts/orphans-native-net10/OrmLiteDemo orphans postgres
```

For .NET 11 use its SDK, `-p:DemoTfm=net11.0` and a separate `artifacts/orphans-native-net11` output directory.
The same chapter, models and assertions have passed all eight Linux-arm64 combinations of JIT/NativeAOT,
PostgreSQL/SQLite and .NET 10/11. Every run required the public assertions and scratch cleanup to succeed.

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
- the SQLite database is created if it does not exist, with foreign keys enabled unless explicitly disabled in the
  connection string, and a 30 second busy timeout

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
