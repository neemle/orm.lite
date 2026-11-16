# Neemle.Orm.Lite

A small, attribute-driven ORM for PostgreSQL and .NET. You decorate plain classes; a source generator writes the
table mappings, the repository methods and the schema migrations at build time. The runtime that the generated code
calls ships in the same package, with soft delete, a row history that records who changed what, and typed queries
written as lambdas.

Status: pre-release, API unstable. The emitted code shape, the runtime type names and the namespaces may change in
any 0.x release.

## Install

The package is not on a public feed yet. Build it into the local feed folder `artifacts/` (which the repository's
`nuget.config` already names) and reference it from a project:

    ./scripts/pack.sh
    dotnet add package Neemle.Orm.Lite --version 0.1.0-pre
    dotnet add package Npgsql

One package, one version: the generator, the runtime and the build assets come in the same reference. The runtime
needs net10.0; it talks to the database through `System.Data.Common`, and you bring the driver (the demo uses
Npgsql).

## Quickstart (the demo proves it)

You have a PostgreSQL database and a class. You want rows in and out, a schema that creates itself, a trash can
instead of lost data, and a record of who changed what.

    [Table("notes")]
    [SoftDelete]
    public sealed class Note
    {
        [Key] public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? DeletedAt { get; set; }
    }

    await using var source = NpgsqlDataSource.Create(connectionString);
    var db = new OrmContext(new GatedDataSource(source, source, source));
    await MyAppMigrations.RunAsync(db.DataSource);          // creates or upgrades the schema

    await NoteOrm.InsertAsync(db, note);
    var mine = await NoteOrm.Query(db).Where(n => n.TenantId == tenant && n.Title == "first").ToListAsync();
    await NoteOrm.DeleteAsync(db, note.Id);                 // a soft delete: the row goes to the trash
    await NoteOrm.Query(db).RestoreAsync([note.Id], tenant);  // and comes back

Wrap work in `using (DbActor.EnterUser(userId))` and every update, soft delete and restore on a `[SoftDelete]` table
lands in `row_history` with that user. Run the whole walk-through, end to end, against a throwaway PostgreSQL:

    ./scripts/demo.sh

It ends with `demo ok`. The code is in [`demo/`](demo/) (about 100 lines, defaults only) and
[`demo/README.md`](demo/README.md) explains each step and the defaults it relies on.

## How it works

At build time the generator reads the classes marked `[Table]`, `[Repository]` and `[Migration]` and emits, into the
consuming project, one `<Entity>Orm` class per table (insert, update, get, delete, typed `Query`), a schema with
its indexes and history triggers, and an `<AssemblyName>Migrations` class whose `RunAsync` applies every pending
migration, generated and hand-written, in version order, one transaction each, under an advisory lock. Hand-written
migrations are classes with `Up` and `Down` that must say whether they are reversible. Mistakes in the declarations
(an unmapped property type, a `[SoftDelete]` entity without a tenant or a key, an irreversible migration with a
`Down`) are build errors with `ORMLITEnnn` ids, not runtime surprises.

At run time the library holds the attributes, the query layer that turns lambdas into SQL, the migration
infrastructure and the connection gate. The gate stamps the acting user onto every connection it hands out, which
is what lets the history triggers attribute a change to a person; it refuses connection strings that would let a
pooled connection keep the previous caller's stamp. Soft-deleted rows are hidden from queries until you ask for
them or restore them, and a restore brings back the rows that went into the trash in the same transaction.

## Build properties

- `OrmLiteActorSetting`: the PostgreSQL custom setting that carries the acting user, `prefix.name` in lower case
  (default `ormlite.actor`); every assembly that references the runtime registers it at start-up
- `OrmLiteDefaultTenant`: the tenant property a soft-delete entity uses when it does not declare one (default
  `TenantId`)

`ExcludeAssets="analyzers"` on the package reference gives a runtime-only consumer: it still receives the build
assets and so the actor setting, and the generators do not run in it.

## Repository layout

- `src/Neemle.Orm.Lite.Generator`: the analyzer (entity, repository and migration generators and their templates)
- `src/Neemle.Orm.Lite.Runtime`: the runtime assembly (attributes, migrations, query layer, history, soft delete)
- `src/Neemle.Orm.Lite`: the packer: analyzer in `analyzers/dotnet/cs`, runtime in `lib/net10.0`, build assets in
  `buildTransitive`
- `tests/Neemle.Orm.Lite.Tests`: the library's own tests (the generator driven over fixtures, the query layer over an
  in-process SQLite; no database needed)
- `tests/PackConsumer`: the throwaway consumers that `scripts/pack.sh` builds from the packed package
- `demo/`: the demo, a consumer of the package

## Scripts

Each works from a fresh clone, given the .NET SDK version in `global.json`:

    ./scripts/build.sh     build the library and its tests
    ./scripts/test.sh      run the tests (any dotnet test arguments are passed through)
    ./scripts/pack.sh      pack into ./artifacts and prove the package with the consumer matrix
    ./scripts/demo.sh      run the demo against a throwaway PostgreSQL (needs Docker, or set ORMLITE_DEMO_CONNECTION)

The restores use committed lock files; after editing a `PackageReference`, run `./scripts/pack.sh --regenerate-locks`
and `./scripts/build.sh -p:RestoreLockedMode=false` and commit the changed `packages.lock.json` files.
