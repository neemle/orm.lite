# Calculated fields: executable database demo

This example runs the same generated entity readers against SQLite and PostgreSQL, with JIT or NativeAOT.
It uses private packages built from this checkout and does not require a published feature version.
Only `Lower` over a physical text property is supported; see [Note.cs](Note.cs) for a nullable source and
quoted read alias, and [Child.cs](Child.cs)/[Tag.cs](Tag.cs) for nonnullable calculated text.

## Build private packages

Use .NET 10 SDK or newer. Run from the repository root:

```sh
sh examples/calculated/prepare-packages.sh
```

The script packs the current Core, SQLite and PostgreSQL projects into `artifacts/calculated/demo/packages`.
The demo restores those packages through its local NuGet configuration and a separate demo package cache.

## Run with JIT

Use a disposable PostgreSQL database. The fixture resets its `demo_calculated_*` tables and
`schema_migrations`; it creates and deletes its own temporary SQLite file.

```sh
export CALCULATED_POSTGRES='Host=localhost;Username=postgres;Database=calculated_demo'
dotnet run --project examples/calculated/CalculatedDemo.csproj -c Release -- sqlite
dotnet run --project examples/calculated/CalculatedDemo.csproj -c Release -- postgres
```

## Run the same cases with NativeAOT

A native compiler toolchain is required. This command targets Linux ARM64; choose the RID matching the
execution platform. The same `CALCULATED_POSTGRES` setting is used by the native binary.

```sh
dotnet publish examples/calculated/CalculatedDemo.csproj -c Release -r linux-arm64 \
  -p:DemoNative=true -o /tmp/calculated-demo
/tmp/calculated-demo/CalculatedDemo sqlite
/tmp/calculated-demo/CalculatedDemo postgres
```

To use .NET 11, add `-p:DemoTfm=net11.0` to the run or publish command. The library packages remain net10.0.
Every run prints provider/runtime context, then the same deterministic `CALCULATED-DEMO v1` result block.
A failed case throws and exits unsuccessfully; no cases are skipped.

## Full reads, NULL and physical queries

[ReadCases.cs](ReadCases.cs) inserts mixed ASCII case, SQL NULL and empty text, then reads database-computed
values through generated full-entity lists and keyed reads. It checks a missing key and a normal physical
filter/order/custom projection. Non-ASCII case conversion is provider-defined and is not compared here.

## References, collections and links

[RelationCases.cs](RelationCases.cs) reads calculated fields through a child-to-note reference, a note's
child collection, and many-to-many tag hydration. It also checks empty collection/link results.

## Writes and schema

[BoundaryCases.cs](BoundaryCases.cs) assigns false calculated values on insert/update and verifies that a
later read recomputes the database result. It checks physical/index metadata, generated baseline DDL and
the actual database columns: the virtual alias is never a stored column. This example declares no history
or soft-delete model; existing history behavior is outside these demo cases.

## Unsupported operations and redaction

[RefusalCases.cs](RefusalCases.cs) verifies that calculated-field filtering, ordering and custom projection
refuse before SQL. Direct and conditional source redaction also refuse with zero statements. The condition
is deliberately false for these rows, so it cannot be used to evade dependency-aware redaction refusal.

The [entity mapping guide](../../docs/entities.md#calculated-read-fields) describes the declaration and its
storage/security limits. These cases use existing public APIs and introduce no extra expression operations.
