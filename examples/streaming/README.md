# Stream a note export

This standalone console example creates three notes in a fresh scratch database, exports selected rows as CSV, and
prints the results of the four streaming APIs. It uses the packed library and provider packages, with no project
references to their source.

## Run with JIT

From the repository root with a .NET 10 SDK, pack the current library into a local feed and restore that exact version:

```bash
dotnet pack src/Core/Core.csproj -c Release -o artifacts/streaming-feed
dotnet pack src/Sqlite/Sqlite.csproj -c Release -o artifacts/streaming-feed
dotnet pack src/Postgres/Postgres.csproj -c Release -o artifacts/streaming-feed
dotnet restore examples/streaming/StreamingExample.csproj --source artifacts/streaming-feed --source https://api.nuget.org/v3/index.json
dotnet run --project examples/streaming/StreamingExample.csproj
```

SQLite is the default. It uses a new temporary folder with pooling disabled and removes that folder afterward.
To use PostgreSQL, supply a **scratch server** on which your account can create and drop databases:

```bash
export STREAMING_POSTGRES='Host=localhost;Username=postgres;Database=postgres'
dotnet run --project examples/streaming/StreamingExample.csproj -- --postgres
```

The PostgreSQL route creates its own uniquely named `streaming_example_*` database and drops only that database when
finished. Do not use production credentials or a production server for examples. The connection string is never printed.

## Run with NativeAOT

Use the same project and scenarios. With the native build prerequisites for your platform installed, this example
publishes for Linux ARM64; select the runtime identifier that matches the machine where the binary will run:

```bash
dotnet publish examples/streaming/StreamingExample.csproj -c Release -r linux-arm64 -p:PublishAot=true -o artifacts/streaming-native10
artifacts/streaming-native10/StreamingExample
artifacts/streaming-native10/StreamingExample --postgres
```

For .NET 11, use its SDK and add `-p:ExampleTfm=net11.0`, choosing a separate output directory. Both routes use
the same `STREAMING_POSTGRES` scratch-server setting. The application scenarios and output are identical to JIT;
the printed runtime version identifies the runtime actually executing them.

## Observable output

The output includes:

```text
Stored notes: 3
Captured export before a later Take(1):
title,stars
"alpha",5
"beta",3
Projected title: alpha
Projected title: beta
Projected stars total: 8
Nullable estimates: 8 null 13
Defaulted estimates: 8 0 13
Preview: alpha
Preview completion: Disposed; rows=1
Before cancellation: alpha
Canceled prefix: 1; cleanup complete
Borrowed preview: alpha
Borrowed completion: Disposed; rows=1
Borrowed count after disposal: 3; committed=True
Stored notes after reads: 3
Scratch resources removed.
streaming example ok; runtime=...
```

## Feature cases

### Cold captured export

`NoteExport` captures the query before a later `Take(1)` and consumes it with ordinary `await foreach`.

### Projection and NULL values

`ProjectionExport` keeps reader callbacks in persistence composition and demonstrates that a row-column stream
returns all rows, preserving NULL with `int?` and converting it to `0` with `int`.

### Early cleanup and cancellation

`StreamLifetime` retains the typed enumerator, awaits disposal and Completion, and preserves an active consumer error
before raising any safe cleanup-only error. `CancellationExport` observes cancellation after one yielded row.

### Borrowed transaction lifetime

`BorrowedExport` finishes the stream before a normal query and commit on the same unit of work; this is explicit
ordering, not an automatic drain or protection against concurrent legacy commands.

A paused stream can retain a connection, locks, and a snapshot. There is no retry, replay, stronger snapshot,
automatic transaction repair, or guarantee that a failed borrowed transaction remains usable. The one-Current bound
covers ORM row retention, not driver/field/consumer memory. See the [streaming guide](../../docs/streaming.md) for the
full lifetime and privacy contract. The snippets in that guide are independent fragments, not this application's entry point.
