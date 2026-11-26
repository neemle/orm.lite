# Installation

[Documentation](README.md)

## Requirements

- **SDK and target framework:** .NET 10 or later. Packages target `net10.0`.
- **PostgreSQL:** version 16 or later. SQLite needs no separate server.

## Choose a provider package

Three packages, one version. The provider packages depend on the core package at exactly the same version, so a
provider reference brings the core, the generator and the build assets with it.

| Package | What it is | Install |
| --- | --- | --- |
| `Neemle.OrmLite` | the provider-neutral runtime, the standard SQL grammar kit and the source generator | pulled in by either provider |
| `Neemle.OrmLite.Sqlite` | the SQLite provider (Microsoft.Data.Sqlite) | `dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0` |
| `Neemle.OrmLite.Postgres` | the PostgreSQL provider (Npgsql) | `dotnet add package Neemle.OrmLite.Postgres --version 0.1.0` |

Referencing a provider package is enough: the build emits the provider's registration into your project, so
`OrmLiteOptions { Provider = "sqlite" }` (or `"postgres"`) resolves with no further code. There is no default provider; a
provider that is not referenced is a start-up error that names the package to add.

## Build from source

To test unreleased changes, pack from the repository root into a local feed:

```bash
dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1
```

Add that folder as a source in your app’s NuGet configuration. Keep nuget.org enabled for dependencies.
Use the [Git-derived version](development.md#build-and-release-versions) printed by `dotnet pack` when installing
from the local feed.

## Compatibility details

The packages target net10.0, and every consumer CI builds is net10.0 (a plain reference, central package management, a
lock file, `PrivateAssets=all`); a later .NET version is expected to work, and CI runs the tests and the NativeAOT smoke
consumer on both .NET 10 and .NET 11 (see [development](development.md#build-test-pack-demo)). The source
generator is a net10.0 analyzer, so the compiler that loads it must run on .NET 10 or later (the `dotnet` CLI of the
.NET 10 SDK, or an IDE whose compiler host is .NET 10); a compiler hosted on .NET Framework, as in older Visual Studio
builds, cannot load it. The `Neemle.OrmLite` package depends on Neemle.CSharpComposer, which NuGet resolves; the
package holds no copy of it.

The PostgreSQL provider needs PostgreSQL 16 or newer: the JSON comparisons write `pg_input_is_valid`, which the server
has from 16, and the index reconcile writes `CREATE OR REPLACE TRIGGER`, from 14. `OrmLite.OpenAsync` reads the server
version when the provider starts and refuses an older server with a `NotSupportedException` that names the version it
found and the minimum, the way the SQLite engine minimum is checked. A data source you build yourself and hand to the
library without `OpenAsync` is not checked.

Next: [run your first query](quickstart.md).
