# Changelog

All notable changes to the three packages (`Neemle.Orm.Lite`, `Neemle.Orm.Lite.Sqlite`, `Neemle.Orm.Lite.Postgres`)
are listed here. The three packages always carry one version.

## 0.1.0-pre

The first pre-release: the API, the emitted code shape, the runtime type names and the namespaces may change in any
0.x release.

- Attribute-driven mapping with a source generator for entities, repositories and schema migrations.
- Soft delete and a row history that records who changed what.
- Typed queries written as lambdas, on PostgreSQL and on SQLite, with a capability table per provider.
- Every assembly carries the package version; the packages carry XML documentation files, embedded debug
  information, Source Link and a package icon.
