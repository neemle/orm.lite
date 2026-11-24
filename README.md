<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="src/PackageAssets/logo-dark.svg">
    <img src="src/PackageAssets/logo.svg" alt="orm.lite" width="480" height="120">
  </picture>
</p>

# Neemle.OrmLite

Neemle.OrmLite is an attribute-driven ORM for .NET, inspired by [TypeORM](https://typeorm.io/).
It supports PostgreSQL and SQLite. Define your tables as C# classes and use strongly typed
queries to read and write rows.

Use it when you want to keep data access close to your C# model and have the build generate
entity mappings, data access methods, and the initial database schema. The runtime also provides
typed migrations, transactions, soft delete, and row history.

**Status:** Pre-release. The API may change in 0.x releases. The packages are currently built
from source into a local NuGet feed.

- [Install](docs/installation.md) with the .NET 10 SDK.
- [Quickstart](docs/quickstart.md) to create a SQLite database and run your first query.
- [Documentation](docs/README.md) for configuration, feature guides, examples, and reference.

[Changelog](CHANGELOG.md) · [MIT License](LICENSE) · [Third-party notices](THIRD-PARTY-NOTICES.md)
