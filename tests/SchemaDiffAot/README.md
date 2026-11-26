# Schema-diff demo: one scenario, JIT and NativeAOT

This executable uses the public schema-diff API on PostgreSQL and SQLite. The same source and assertions run in JIT and NativeAOT. Each invocation requires a new isolated fixture.

## Cases

### Compare and review

[`DemoScenario.RunAsync`](DemoScenario.cs) initializes an old `items(id, original)` model and an original row. The desired model adds the nullable `added` field and a simple `new_items` table. Public `ObserveAsync`, `Compare` and `Emit` must report exactly those two changes without changing history or the original row.

#### SQLite primary-key nullability

The SQLite route also runs seven quiet checks in separate owned temporary databases before the additive scenario. An ordinary `TEXT PRIMARY KEY` and an inline `INTEGER PRIMARY KEY DESC` can store NULL: observation must retain that fact, comparison with a required nonnullable key must report `NullabilityChange`/`ManualRequired`, and emission must refuse. Explicit `TEXT PRIMARY KEY NOT NULL`, an `INTEGER` rowid alias and table-level `PRIMARY KEY(id DESC)` retain nonnullable no-op behavior. `STRICT` and `WITHOUT ROWID` shapes remain manual-required under the existing supported-shape boundary.

These checks use the same [permanent regression contract](../Tests/SchemaDiff/SqliteNullabilityContract.cs) as the focused xUnit cases. They verify actual NULL insertion/storage and public observation/comparison/refusal, remove their fixtures, and leave the original `PARITY` output unchanged. To run only this compare/review case, pass `sqlite-nullability` as the executable's sole argument in either JIT or NativeAOT.

### Apply and repeat

The explicit fixture runner applies the reviewed artifact. Public observation and comparison must then return `NoOp`; attempting to emit that no-op must refuse. The old row retains its original value, and its new field is NULL. Repeating application must report `AlreadyApplied` with no new unit or history entry.

### Down and up

[`DownUpAsync`](DemoScenario.cs) inserts values into the new field and table, executes the explicit fixture Down, and checks the original schema, original row and original history. Applying Up again must restore the desired schema and return a public no-op on re-comparison.

**Down loses the new field and table data.** The demo checks that loss explicitly. Back up and restore those values if they must survive. Fixture execution does not grant permission to run destructive changes against a deployed database.

## Run

Run from the repository root. Use containers for builds and execution. A standard checkout mounts its Git metadata with the repository; a Git worktree also needs its absolute Git common directory mounted at the same path.

```sh
docker run --rm -v "$PWD:/repo" -w /repo mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build tests/SchemaDiffAot/SchemaDiffAot.csproj -c Release

docker run --rm -v "$PWD:/repo" -w /repo mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet tests/SchemaDiffAot/bin/Release/net10.0/Neemle.OrmLite.SchemaDiffAot.dll \
  sqlite /repo/artifacts/schema-diff/sdet/example-jit.sqlite
```

For PostgreSQL, connect only to a disposable PostgreSQL 19 fixture. Set `ORMLITE_TEST_POSTGRES` to that fixture's administrative connection string and pass a fresh database name beginning with `parity117_`. The executable creates that database and leaves it for inspection; remove the owned server after verification. For example, on a Docker network containing an isolated server named `parity-pg`:

```sh
docker run --rm --network YOUR_FIXTURE_NETWORK \
  -e 'ORMLITE_TEST_POSTGRES=Host=parity-pg;Username=postgres;Database=postgres' \
  -v "$PWD:/repo" -w /repo mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet tests/SchemaDiffAot/bin/Release/net10.0/Neemle.OrmLite.SchemaDiffAot.dll \
  postgres parity117_example_jit
```

Publish with an SDK image containing the NativeAOT native toolchain; select the RID matching that image:

```sh
docker run --rm -v "$PWD:/repo" -w /repo YOUR_AOT_SDK_IMAGE \
  dotnet publish tests/SchemaDiffAot/SchemaDiffAot.csproj -c Release \
  -r linux-arm64 -p:PublishAot=true -p:IlcMaxParallelism=1 \
  -o artifacts/schema-diff/sdet/native10

docker run --rm -v "$PWD:/repo" -w /repo YOUR_AOT_SDK_IMAGE \
  artifacts/schema-diff/sdet/native10/Neemle.OrmLite.SchemaDiffAot \
  sqlite /repo/artifacts/schema-diff/sdet/example-aot.sqlite
```

Use `-p:DemoTfm=net11.0` with SDK 11 to build the same source for runtime 11. Pass PostgreSQL's environment variable and network to the native invocation exactly as for JIT.

## Compare results

Successful runs print `PARITY` lines for the neutral migration version, verified schema, preserved and intentionally lost data, exact history versions, and outcome sequence. Those lines must match byte for byte across both providers and execution modes. No provider, runtime, timestamp or database identifier is included in those result lines.

Runtime/source/binary attribution, exact executed commands and captured outputs live in [`artifacts/schema-diff/sdet/`](../../artifacts/schema-diff/sdet/). Existing producer static-factory compilation evidence remains under `artifacts/schema-diff/dev/`; this demo adds direct public observation, emission and re-comparison in NativeAOT.
