# Neemle.OrmLite

**A source-generated ORM for .NET 10, PostgreSQL, and SQLite.** Inspired by TypeORM.

Define tables with C# attributes. ORM.lite generates typed repositories and an initial schema at build time.
Query with LINQ expressions, write migrations in C#, and enable soft delete and row history where needed.

## Status

Version **0.1.0** is the first public release. The API may change in 0.x versions.
SQLite needs no database server. PostgreSQL requires version 16 or later.

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

For SQLite:

```bash
dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0
```

For PostgreSQL:

```bash
dotnet add package Neemle.OrmLite.Postgres --version 0.1.0
```

Referencing a provider package is enough: the build emits the provider's registration into your project, so
`OrmLiteOptions { Provider = "sqlite" }` (or `"postgres"`) resolves with no further code. There is no default
provider; a provider that is not referenced is a start-up error that names the package to add.

## Quickstart

Create a .NET 10 console app with SQLite:

```bash
dotnet new console -n Notes -f net10.0
cd Notes
dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0
```

Create `Note.cs`.

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

Replace `Program.cs`.

```csharp
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Notes;

await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = "notes.db" });
await db.MigrateAsync();

await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "hello", Stars = 5 });
var best = await NoteOrm.Query(db).Where(n => n.Stars >= 3).ToListAsync();
foreach (var note in best) Console.WriteLine(note.Title);
```

Run `dotnet run`. The program creates `notes.db`, inserts a note, and prints `hello`.
`NoteOrm` is generated from `Note`. `MigrateAsync()` applies the generated baseline and any registered migrations.

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
[provider reference](https://github.com/neemle/orm.lite/blob/HEAD/docs/providers.md).

## Documentation

The documentation is kept in the public repository, which is the single home of the user documentation:

- [Documentation](https://github.com/neemle/orm.lite/tree/HEAD/docs): installation, configuration, guides,
  and reference.
- [Demo](https://github.com/neemle/orm.lite/tree/HEAD/demo): a runnable program that walks the library chapter by
  chapter on SQLite and on PostgreSQL, with comments in every file.

## License

MIT, see the [license](https://github.com/neemle/orm.lite/blob/HEAD/LICENSE) in the repository; third-party
components are listed in the [notices](https://github.com/neemle/orm.lite/blob/HEAD/THIRD-PARTY-NOTICES.md).
