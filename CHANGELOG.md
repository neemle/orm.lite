# Changelog

All notable changes to the three packages (`Neemle.OrmLite`, `Neemle.OrmLite.Sqlite`, `Neemle.OrmLite.Postgres`)
are listed here. The three packages always carry one version.

## Unreleased

### Added

- `OrmContext.Maintenance` runs explicit database maintenance through the optional `IMaintenanceProvider` port
  ([maintenance](docs/maintenance.md)). `CheckpointWalAsync` checkpoints a SQLite WAL in an explicit mode (`Passive`,
  `Full`, `Restart`, `Truncate`) and returns a structured `WalCheckpointResult`; PostgreSQL refuses it
  (`Capability.WalCheckpoint` is `None`). Diagnostics report maintenance statements as `StatementKind.Maintenance` with
  `StatementEvent.Operation` naming the operation.
- `OrmContext.Maintenance.VacuumAsync` runs VACUUM in an explicit `VacuumMode` (`Plain`, or `Full` on PostgreSQL), of
  the whole database or of one table on PostgreSQL. On SQLite it checkpoints the WAL (`Truncate`) first and reports
  `NotRun` when a reader holds the log. New capabilities `Vacuum`, `VacuumFull`, `VacuumTable`; `PostgresProvider` now
  implements `IMaintenanceProvider` (`DefaultMaintenanceTimeout` 30 s, a `lock_timeout` guard, SQLSTATE 55P03 reported as
  `Busy`). `ThrowIfNotComplete()` turns an incomplete result into `MaintenanceIncompleteException`.
- `OrmContext.Maintenance.AnalyzeAsync` refreshes planner statistics for the whole database or one table
  (`AnalyzeScope`), on SQLite and PostgreSQL (`Capability.Analyze`). SQLite takes the writer gate; contention is `Busy`.

### Dependencies

- `Neemle.OrmLite` now depends on Neemle.CSharpComposer 0.6.0 (was 0.5.0), which the generator renders through. Its
  [release notes](https://github.com/neemle/cs-composer/releases/tag/v0.6.0) list a parse nesting limit, parser fixes
  and edit layout taken from the source. The existing generator tests and corpus pass unchanged.

## 0.1.0

The first public release. The API, the emitted code shape, the runtime type names and the namespaces may change in any
0.x release.

- Attribute-driven mapping with a source generator for entities, repositories and schema migrations.
- Soft delete and a row history that records who changed what.
- Typed queries written as lambdas, on PostgreSQL and on SQLite, with a capability table per provider.
- Every assembly carries the package version; the packages carry XML documentation files, embedded debug
  information, Source Link and a package icon.

### Breaking changes

Rename. The root namespace, the package ids and the assembly names are now `Neemle.OrmLite`: the packages are
`Neemle.OrmLite`, `Neemle.OrmLite.Sqlite` and `Neemle.OrmLite.Postgres`, the assemblies are `Neemle.OrmLite.Runtime`,
`Neemle.OrmLite.Sql` and the provider assemblies, the namespaces are `Neemle.OrmLite`, `Neemle.OrmLite.Sql`,
`Neemle.OrmLite.Sqlite`, `Neemle.OrmLite.Postgres` and `Neemle.OrmLite.Nodes`, and the generated code lives in
`Neemle.OrmLite.Generated`. Migration: replace the prefix `Neemle.Orm.Lite` by `Neemle.OrmLite` in package references,
`using` directives, `InternalsVisibleTo` names and any text that names a type of the library. Nothing else changes: type
and member names, the `OrmLite*` settings and MSBuild properties, the capability manifest names and every generated
statement are the same. There is no shim and no package under the old ids, because the earlier development packages were never published.

The core (`Neemle.OrmLite`) no longer carries any PostgreSQL text, type, id or hash input: every provider speaks
through `ISqlProvider`, and the legacy PostgreSQL dialect path that ran beside it is gone. These changes are made
without `Obsolete` shims, because the earlier development packages were never published.

Removed or changed public API:

- Migration host. `MigrationRunner.LockKey`, `MigrationRunner.LockMigrations()` and
  `MigrationRunner.UnlockMigrations()` are removed. The exclusive run lock and the history table bootstrap now come
  from the provider's planner: `MigrationRunPlan` and `IMigrationPlanner.PlanRun(ISqlProvider)` are added (the default
  is a lock-free plan), and `StatementPlanner` is public. The PostgreSQL package owns the advisory lock key.
- Actor stamp. `DbActor.StampStatement`, `DbActor.StampAsync` and `OrmLiteSettings.StampStatement` are removed. The
  statement that stamps the acting user is written by the provider and run through `ISqlProvider.StampActorAsync`.
- Dialect path. `ISqlDialect`, `PgDialect`, `IDdlDialect` and `PgDdlDialect` are removed, and `OrmContext` is created
  with `OrmContext(DbDataSource, ISqlProvider)` only (the dialect parameter and `OrmContext.Dialect` are gone).
  `MigrationBuilder()`, `MigrationBuilder(IDdlDialect)` and `MigrationBuilder.Operations` are removed; use
  `MigrationBuilder(ISqlProvider)` and `Render(ISqlProvider)`. `TableBuilder()` and `TableBuilder(IDdlDialect)` are
  removed and `TableBuilder(ISqlProvider)` is added. `SoftDeleteSql.SetClause` and `SoftDeleteSql.LiveTerm` take an
  `ISqlProvider`. `Jsonb.ColumnType` and `Jsonb.EmptyDocumentDefaultSql` are removed; the provider writes the type and
  the empty-document default.
- Provider ids and history DDL. `ProviderRegistry.PostgresId` is removed. `RowHistoryDdl.Preamble` is now a method
  taking an `ISqlProvider`, `RowHistoryDdl.Reconcile(ISqlProvider, tables)` takes the provider, `RowHistoryDdl.SetHash`
  needs no provider, and `RowHistoryDdl.SetHash(string actorSetting, tables)` is added.
- Catalog. The `PostgresCatalog` constructor takes the provider as its first argument:
  `PostgresCatalog(ISqlProvider, DbConnection, DbTransaction?)`. Use `ISqlProvider.CreateCatalog` rather than the
  constructor.

Behaviour changes:

- Refusing a capability that a provider does not support now throws `CapabilityNotSupportedException` (it still derives
  from `NotSupportedException`, so a `catch (NotSupportedException)` keeps working). Its message names the provider, the
  capability, the operation and the table, and is no longer worded by the dialect; code that matches the message text
  needs updating.
- The message of the refusal of a null value in `Db.JsonArrayContainsString` no longer names a PostgreSQL function.
- The migration identity is the same on every provider and whichever packages are loaded. The versions
  `9900_orm_index_reconcile_*` and `9900_orm_history_reconcile_*` are renamed (their hashes are computed from the
  declarations, not from rendered PostgreSQL text). On every existing PostgreSQL database the index reconcile and the
  history install therefore run once again on upgrade; both are idempotent, and each records one new row in
  `schema_migrations` beside the old one. A non-default actor setting (`OrmLiteSettings.ActorSetting`) is now part of
  the history version, so changing it installs the history function again. SQLite databases already used this form.
