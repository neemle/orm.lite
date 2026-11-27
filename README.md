<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="src/PackageAssets/logo-dark.svg">
    <img src="src/PackageAssets/logo.svg" alt="orm.lite" width="480" height="120">
  </picture>
</p>

# Neemle.OrmLite

**A source-generated ORM for .NET 10, PostgreSQL, and SQLite.** Inspired by [TypeORM](https://typeorm.io/).

Model your tables with C# attributes. ORM.lite generates typed repositories and an initial schema at build time.
Use LINQ expressions to filter rows, define migrations in C#, and use soft delete with row history when you need it.

## Get started

Choose the provider for your database. Each provider brings in the matching core package.

| Database | NuGet package |
| --- | --- |
| SQLite | [Neemle.OrmLite.Sqlite](https://www.nuget.org/packages/Neemle.OrmLite.Sqlite/0.1.0) |
| PostgreSQL 16+ | [Neemle.OrmLite.Postgres](https://www.nuget.org/packages/Neemle.OrmLite.Postgres/0.1.0) |

```bash
dotnet add package Neemle.OrmLite.Sqlite --version 0.1.0
```

Follow the [SQLite quickstart](docs/quickstart.md) to create a table and run your first query.

- [Relations](docs/relations.md) covers navigation properties, join tables, and foreign keys.
- [Explicit graph saves](demo/GraphSave/README.md) demonstrates atomic related inserts and updates with the current checkout's packages.
- [Typed joined reads](demo/README.md#typed-joined-reads) demonstrates INNER/LEFT/self joins, explicit result cells and joined-row paging, with working cases and JIT/NativeAOT commands for the current checkout.
- [Streaming reads](docs/streaming.md) covers cold row consumption, early cleanup, and transaction lifetime.
- [Migrations](docs/migrations.md) shows how to change a schema and roll back a migration.
- [Provider capabilities](docs/providers.md) compares PostgreSQL and SQLite behavior.
- [Full documentation](docs/README.md) includes configuration, queries, diagnostics, and examples.

Version **0.1.0** is the first public release. The API may change during the 0.x series.

[Changelog](CHANGELOG.md#010) · [MIT License](LICENSE) · [Third-party notices](THIRD-PARTY-NOTICES.md)
