# Neemle.Orm.Lite demo

You have a PostgreSQL database and a C# class. You want rows going in and out, a schema that creates itself, a
trash can instead of lost data, and a record of who changed what. You add one package, put three attributes on the
class, and it works. This folder is that, end to end, in about 100 lines.

## The whole idea

1. Add the package. The generator and the runtime come in one reference; there is nothing else to install.

       dotnet add package Neemle.Orm.Lite
       dotnet add package Npgsql

   The package is not published yet: until it is, `./scripts/pack.sh` builds it into `artifacts/`, a local feed
   that the root `nuget.config` already names (see "Run it" below).

2. Describe the table by decorating a class (`Note.cs`).

       using Neemle.Orm.Lite;

       [Table("notes")]
       [SoftDelete]
       public sealed class Note
       {
           [Key] public Guid Id { get; set; }
           public Guid TenantId { get; set; }
           public string Title { get; set; } = "";
           public DateTime? DeletedAt { get; set; }
       }

3. Start up (`Program.cs`). One data source, one context, one call that creates or upgrades the schema.

       using Neemle.Orm.Lite;
       using Neemle.Orm.Lite.Generated;
       using Npgsql;

       await using var source = NpgsqlDataSource.Create(connectionString);
       var db = new OrmContext(new GatedDataSource(source, source, source));
       await OrmLiteDemoMigrations.RunAsync(db.DataSource);

4. Use it. Every table gets a generated `NoteOrm` class.

       await NoteOrm.InsertAsync(db, note);
       var mine = await NoteOrm.Query(db).Where(n => n.TenantId == tenant && n.Stars >= 3).ToListAsync();
       await NoteOrm.DeleteAsync(db, note.Id);                       // a soft delete: the row goes to the trash
       await NoteOrm.Query(db).RestoreAsync([note.Id], tenant);      // and comes back

Who did it is recorded for you: wrap work in `using (DbActor.EnterUser(userId))` and every change to a
`[SoftDelete]` table lands in `row_history` with that user.

## What the demo shows

| Step | What happens |
| --- | --- |
| 1 | the generated migrations create the table, the unique-while-live index and the history triggers |
| 2 | `WelcomeNote` (an explicit, reversible `[Migration]`) seeds a row |
| 3 | insert, update and a lambda query, with no SQL written |
| 4 | a soft delete hides the row from queries; `IncludingDeleted()` still sees it |
| 5 | the row is restored; the history lists the update, the delete and the restore with the acting user |

## Defaults you did not have to set

- the soft-delete tenant column is `TenantId` (override with the `OrmLiteDefaultTenant` build property, or per
  entity with `[SoftDelete(Tenant = "OwnerId")]`; `SoftDeleteAttribute.Platform` means no tenant at all)
- the soft-delete column is `DeletedAt` (`[SoftDelete("RemovedAt")]` names another property)
- the PostgreSQL setting that carries the acting user is `ormlite.actor` (override with the
  `OrmLiteActorSetting` build property, a lowercase `prefix.name`)
- table and column names are the snake_case of the class and property names

## Things that are worth knowing before you copy this

**The three `GatedDataSource` arguments are three connection pools, one per lane: `request`, `background` and
`ops`.** Code runs on the background lane unless it wraps itself in `DbLane.Enter(DbLaneKind.Request)` (or
`Ops`), which switches the pool every connection in that scope comes from. A small program needs one pool, so the
demo passes the same data source three times; a service that wants its request traffic kept apart from its jobs
passes three differently sized data sources. Connection strings that set `No Reset On Close` are refused, because
a pooled connection would then keep the previous caller's acting user.

**`[SoftDelete]` needs three properties on the entity**, and the build fails with a diagnostic when one is
missing: a non-nullable `Guid` key (`ORMLITE008`), a `Guid` tenant property, `TenantId` unless you name another
(`ORMLITE007`), and a `DateTime?` soft-delete property, `DeletedAt` unless you name another (`ORMLITE002`).

**`[Index]` takes column names, everything else takes property names.** The demo's `[Index("tenant_id", "title",
Unique = true, LiveOnly = true)]` names the snake_case columns, while `Tenant = "OwnerId"` and
`[SoftDelete("RemovedAt")]` name C# properties. `LiveOnly` makes a unique index that only counts rows that are
not in the trash (`ORMLITE004` when it is not unique or the entity is not `[SoftDelete]`).

**An explicit `[Migration]` runs between the generated ones.** The generated `RunAsync` collects every migration,
generated and explicit, and applies the ones not yet recorded in `schema_migrations` in ordinal order of their
version string, one transaction each, under an advisory lock. The generated baseline is `0000_...` and the
generated index migration `0001_...`; the index and history reconciliations that finish a run start with `9900_`.
So an explicit migration whose version sorts between them, like `0100_welcome_note`, runs after the tables exist
and before the reconciliations. Every `[Migration]` must name `Reversibility.Reversible` or
`Reversibility.Irreversible` (`ORMLITE006` otherwise), and an irreversible one must not override `Down`
(`ORMLITE003`).

**History records changes, not creation.** The `row_history` triggers fire on update, soft delete and restore, so
an insert is not in the history, and hard deleting a row removes that row's history. The acting user is whatever
`DbActor.EnterUser` set for the connection; a change made outside it is still recorded, with actor kind `unknown`
and no user (`DbActor.EnterSystem` records kind `system`).

**The migrations class is named after the assembly.** The demo's assembly is `OrmLiteDemo`, so the generated
class is `OrmLiteDemoMigrations`; in your project it is `<YourAssemblyName>Migrations` in the `Neemle.Orm.Lite.Generated`
namespace (letters and digits of the assembly name only).

## Run it

You need the .NET SDK pinned by `../global.json` and Docker (for a PostgreSQL you can throw away). From the
repository root:

    ./scripts/demo.sh

It packs the library into `artifacts/` when the package is not there yet, starts a PostgreSQL container, runs this
demo against it as a consumer of the package (no project reference), checks that the last line is `demo ok` and
removes the container.

To use a database you already have, set the connection string and the script starts nothing:

    ORMLITE_DEMO_CONNECTION="Host=localhost;Username=postgres;Database=postgres" ./scripts/demo.sh

The demo is also a plain console project: after `./scripts/pack.sh` has produced the package,
`dotnet run --project demo` runs it against `ORMLITE_DEMO_CONNECTION` (default
`Host=localhost;Username=postgres;Database=postgres`).
