# Foreign-key database demo

Run one generated-model application with JIT or NativeAOT on PostgreSQL and SQLite. Private packages come
from this checkout; this example makes no published-version claim.

## Prepare and run

Use .NET 10 SDK or newer. From the repository root:

```sh
sh examples/foreign-keys/prepare-packages.sh
export FOREIGN_KEYS_POSTGRES='Host=localhost;Username=postgres;Database=foreign_keys_demo'
dotnet run --project examples/foreign-keys/ForeignKeysDemo.csproj -c Release -- sqlite
dotnet run --project examples/foreign-keys/ForeignKeysDemo.csproj -c Release -- postgres
```

Use a disposable PostgreSQL database: the fixture resets its `demo_fk_*` tables and `schema_migrations`.
SQLite uses a temporary file and removes it on completion. Packages/build outputs stay under
`artifacts/foreign-keys/demo-parity`; the demo has its own NuGet cache.

## NativeAOT

Install the native compiler toolchain. This example targets Linux ARM64; choose the RID for the execution
platform. The native binary uses the same PostgreSQL environment setting and runs the same cases.

```sh
dotnet publish examples/foreign-keys/ForeignKeysDemo.csproj -c Release -r linux-arm64 \
  -p:DemoNative=true -o /tmp/foreign-keys-demo
/tmp/foreign-keys-demo/ForeignKeysDemo sqlite
/tmp/foreign-keys-demo/ForeignKeysDemo postgres
```

For .NET 11 add `-p:DemoTfm=net11.0` to run/publish. The libraries stay net10.0. Every run must finish with
the same `FOREIGN-KEY-DEMO v1` result block and `PASS`; a failed case throws and exits unsuccessfully.

## Declarations and exact catalogs

[Parent.cs](Parent.cs), [SetNullChild.cs](SetNullChild.cs) and [Tag.cs](Tag.cs) show navigation annotations,
identical inverse/reference declarations and independent source/target link actions. Both referenced link
keys are Guid. [CatalogCases.cs](CatalogCases.cs) checks seven exact names/endpoints/actions, including a
quoted constraint name, one deduplicated inverse/reference FK and two separately owned link constraints.
[LoaderChild.cs](LoaderChild.cs) has a loader without FK opt-in and accepts an orphan.

## Generated reads, writes and calculated fields

[GeneratedCases.cs](GeneratedCases.cs) uses generated insert/update/keyed/reference/collection readers,
valid and nullable updates, orphan insert/update rejection and physical SetNull deletion. The FK-declared
[Parent](Parent.cs) also has `CalculatedLower`: the generated reader returns `mixed` from `MiXeD`.
[LinkCases.cs](LinkCases.cs) exercises the merged typed linked reader with a calculated tag value, both link
orphan directions, source Restrict and target Cascade. A supplied virtual value is ignored by writes.

## Physical actions and soft deletion

[ActionCases.cs](ActionCases.cs) demonstrates Cascade, Restrict and NoAction with real physical parent
deletes. [SoftDeleteCases.cs](SoftDeleteCases.cs) shows that an ORM soft-delete update keeps its child,
while a later physical delete cascades. Providing a navigation object does not implicitly save its parent.

## Fresh baseline and existing databases

[UpgradeCases.cs](UpgradeCases.cs) records an old baseline without a constraint, stores an orphan, then
adds FK metadata under the same schema token. The catalog, data and history remain unchanged: existing
databases require an explicit reviewed migration and data validation. Re-running the generated fresh
schema also leaves migration history and constraint counts unchanged.

[RefusalCases.cs](RefusalCases.cs) demonstrates nonnullable SetNull and self-cycle rejection before SQL.
Other unsupported shapes include unmanaged/cross-baseline targets and non-Guid pure generated links;
this example adds no automatic upgrade or repair mechanism.

## SQLite configuration

[ConfigurationCases.cs](ConfigurationCases.cs) reads `PRAGMA foreign_keys` for enabled SQLite use and
observes native violations on both providers. A separate SQLite-only observation opens the same file with
explicit `Foreign Keys=False`, checks PRAGMA0, accepts an orphan and deletes that row. It is labelled
**unenforced**, even though constraints exist. This provider-specific line is outside the shared result
block; PostgreSQL has no corresponding SQLite connection option. No case silently toggles the main fixture.

See the [relation guide](../../docs/relations.md) for syntax, supported mappings and explicit migrations.
