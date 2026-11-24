# Configure the connection

[Documentation](README.md)

## Connection options

| Option | Applies to | Purpose |
| --- | --- | --- |
| `Provider` | Both | Required. Use `sqlite` or `postgres`. |
| `Path` | SQLite | Database file path. |
| `Host`, `Port`, `Database`, `Username`, `Password` | PostgreSQL | Server address, database, and credentials. |
| `ConnectionString` | Both | Provider-specific connection settings. |
| `Pooling` | Both | Enable or disable connection pooling. |
| `BusyTimeoutSeconds` | SQLite | How long to wait for a busy database. Default: 30 seconds. |
| `Diagnostics` | Both | [Statement and transaction reporting](diagnostics.md). |

## Settings sources

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

Add the PostgreSQL package from the [local feed](installation.md). Then set the provider and connection options in
`appsettings.json`. The same application code opens the new provider when it calls `OrmLiteOptions.Load()`:

    dotnet add package Neemle.OrmLite.Postgres --prerelease

    { "OrmLite": { "Provider": "postgres", "Host": "localhost", "Port": 5432, "Username": "postgres", "Database": "mydb" } }

`Provider` names the database and has no default: leaving it out is a start-up error that lists the installed
providers, and naming one whose package is not referenced is a start-up error that names the package to add.
`ConnectionString` is accepted by both providers for settings the discrete fields do not cover; fields a provider
does not use (`Path` for PostgreSQL, `Host` for SQLite) are refused by name, not ignored.

For compile-time options, see [Build properties](build-properties.md).
