# Neemle.OrmLite

A small, attribute-driven ORM for .NET on PostgreSQL or SQLite. You decorate plain classes; a source generator writes
the table mappings, the entity methods and the schema migrations at build time. The runtime that the generated code
calls ships in the same package, with soft delete, a row history that records who changed what, and typed queries
written as lambdas.

Inspired by TypeORM, with an attribute-driven .NET API and build-time code generation.

## Status

Pre-release, API unstable. The emitted code shape, the runtime type names and the namespaces may change in any 0.x
release. The packages target .NET 10 and are built and used there in CI; the CI tests and the NativeAOT smoke consumer
run on both .NET 10 and .NET 11, and a later .NET version is expected to work.

Native AOT: the runtime and the provider libraries are built with the AOT and trim analyzers on, so a warning fails
their build. CI publishes one small NativeAOT consumer and runs it on .NET 10 and .NET 11; beyond that, AOT
compatibility is declared by the build and not demonstrated.

## Install

Three packages, one version. Add the provider you use: it depends on the core package at exactly the same version,
so a provider reference brings the core, the generator and the build assets with it.

| Package | What it is |
| --- | --- |
| `Neemle.OrmLite.Sqlite` | the SQLite provider (Microsoft.Data.Sqlite) |
| `Neemle.OrmLite.Postgres` | the PostgreSQL provider (Npgsql) |
| `Neemle.OrmLite` | the runtime, the standard SQL grammar kit and the source generator; pulled in by either provider |

```
dotnet add package Neemle.OrmLite.Sqlite --prerelease
dotnet add package Neemle.OrmLite.Postgres --prerelease
```

Referencing a provider package is enough: the build emits the provider's registration into your project, so
`OrmLiteOptions { Provider = "sqlite" }` (or `"postgres"`) resolves with no further code. There is no default
provider; a provider that is not referenced is a start-up error that names the package to add.

## Quickstart

You have a class and you want rows in and out of a database, with no server to run and no SQL to write. This is the
whole program, on SQLite.

Describe a table by decorating a class (`Note.cs`):

```csharp
using Neemle.OrmLite;

namespace Notes;

[Table("notes")]
public sealed class Note
{
    [Key] public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int Stars { get; set; }
}
```

Open the database, create the schema, use it (`Program.cs`):

```csharp
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Notes;

await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = "notes.db" });
await db.MigrateAsync();

await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "hello", Stars = 5 });
var best = await NoteOrm.Query(db).Where(n => n.Stars >= 3).ToListAsync();
```

Running it creates `notes.db`, creates the table, inserts the row and reads it back. The repository `NoteOrm` is
generated from the class, and `db.MigrateAsync()` applies the generated baseline and your own `[Migration]` classes.

## Providers

The provider is a setting, not code. `OrmLiteOptions.Load()` reads the `OrmLite` block of `appsettings.json` and
lets `OrmLite__*` environment variables override it setting by setting:

```json
{ "OrmLite": { "Provider": "sqlite", "Path": "notes.db" } }
```

```csharp
await using var db = await OrmLite.OpenAsync(OrmLiteOptions.Load());
```

To switch to PostgreSQL, reference `Neemle.OrmLite.Postgres` as well and change the block; no code changes:

```json
{ "OrmLite": { "Provider": "postgres", "Host": "localhost", "Port": 5432, "Username": "postgres", "Database": "mydb" } }
```

Every provider declares, for each capability, whether the database does it natively, whether the provider emulates
it with the same or stronger meaning, or whether it is not available. A declaration or call that needs a capability
the provider does not have is refused by name before any statement runs, and at build time when the project's
providers are known. The capability table and the other provider differences are in the
[repository README](https://github.com/neemle/orm.lite#capabilities-per-provider).

## Documentation

The documentation is kept in the public repository, which is the single home of the user documentation:

- [README](https://github.com/neemle/orm.lite#readme): configuration, capabilities per provider, migrations,
  limitations and build properties.
- [Demo](https://github.com/neemle/orm.lite/tree/HEAD/demo): a runnable program that walks the library chapter by
  chapter on SQLite and on PostgreSQL, with comments in every file.

## License

MIT, see the [license](https://github.com/neemle/orm.lite/blob/HEAD/LICENSE) in the repository; third-party
components are listed in the [notices](https://github.com/neemle/orm.lite/blob/HEAD/THIRD-PARTY-NOTICES.md).
