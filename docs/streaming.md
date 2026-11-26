# Streaming query rows

[Documentation](README.md) · [Queries](queries.md)

Use a stream when you want to process rows as they arrive instead of building a result list. A `QueryStream<T>` is a
cold `IAsyncEnumerable<T>`: creating it compiles and captures the query, and the first advance starts database work.
The `Async` suffix does not mean the terminal returns a `Task`; do not `await` the call that creates the stream.

The independent snippets below use the `Note` model, generated `NoteOrm`, and open `db` from the
[quickstart](quickstart.md). They assume the same imports and an application `CancellationToken ct`.

## Run the complete example

The [streaming export example](../examples/streaming/README.md) runs the same scenarios with SQLite or PostgreSQL:

- [Cold captured export](../examples/streaming/README.md#cold-captured-export)
- [Projection and NULL values](../examples/streaming/README.md#projection-and-null-values)
- [Early cleanup and cancellation](../examples/streaming/README.md#early-cleanup-and-cancellation)
- [Borrowed transaction lifetime](../examples/streaming/README.md#borrowed-transaction-lifetime)

Follow its [JIT commands](../examples/streaming/README.md#run-with-jit) or
[NativeAOT commands](../examples/streaming/README.md#run-with-nativeaot) to run the application.

## Consume rows with await foreach

```csharp
var stream = NoteOrm.Query(db)
    .Where(n => n.Stars >= 3)
    .OrderBy(n => n.Title)
    .StreamAsync(ct);

await foreach (var note in stream)
{
    Console.WriteLine(note.Title);
}
```

Each successful advance reads and maps one row. While your loop body is running or awaiting other work, the ORM does
not ask for the next row. It performs a final read to discover EOF. A normal enumeration executes one read
statement, including an empty result or `Take(0)`; it does not page, retry, or load navigations behind the loop.

`await foreach` awaits disposal on normal exit, `break`, cancellation, and a body exception. However, disposal attempts
and successful cleanup are different: resource cleanup errors do not throw from `DisposeAsync`. When early cleanup
success matters, retain the typed enumerator and inspect its [Completion](#observe-early-cleanup) explicitly.

## Choose a row shape

All four terminals return typed row streams immediately and accept an optional final `CancellationToken`.
`Query<T>` remains unconstrained; streaming uses the same supported mapped entities, views, types, visibility, masks,
filters, ordering, and paging as the corresponding buffered read.

| Terminal | Row value | Corresponding read behavior |
| --- | --- | --- |
| `StreamAsync(ct)` | `T`, using the query's existing map | `ToListAsync()` |
| `StreamProjectAsync(columns, map, ct)` | Detached mapped value | `ProjectAsync` |
| `StreamProjectionAsync(map, ct)` | Configured projection, mapped to a detached value | `ProjectionAsync` |
| `StreamColumnAsync<TCol>(column, ct)` | Converted column value per row | Row projection; no first-row limit |

`StreamProjectionAsync` reads terms configured by `ProjectColumn` and `ProjectPredicate`.
Projection callbacks receive a `DbDataReader`. Compose those callbacks in your persistence layer and expose detached
values to business code, just as for buffered projections:

```csharp
var titles = NoteOrm.Query(db)
    .StreamProjectAsync([n => n.Title], reader => reader.GetString(0), ct);

var configuredStars = NoteOrm.Query(db)
    .ProjectColumn(n => n.Stars)
    .StreamProjectionAsync(reader => reader.GetInt32(0), ct);

var stars = NoteOrm.Query(db).StreamColumnAsync<int?>(n => n.Stars, ct);
```

`StreamColumnAsync` returns every selected row within the ordinary order/page. SQL NULL converts to `default(TCol)`:
for example, `int` receives `0`, while `int?` receives `null`. Choose a nullable target when you need to preserve NULL.
An empty result yields no values. Other conversions follow the
[scalar conversion rules](queries.md#read-a-single-value).
There is no new grouped or aggregate stream terminal. Existing single-value, grouped, buffered, and write APIs
keep their own behavior.

A custom map runs inline and must return detached values. Copy reader-owned mutable data you need later; do not
retain the reader, reader-backed streams or row views, or reuse one mutable DTO/buffer for later rows. The ORM captures
the map delegate, not the mutable application state its closure may use. Your map's thread safety remains your
responsibility.

## Capture once, enumerate afresh

```csharp
var query = NoteOrm.Query(db).OrderBy(n => n.Title);
var captured = query.StreamAsync(ct);
query.Take(1);

await foreach (var note in captured)
{
    Console.WriteLine(note.Title);
}
```

The captured stream retains the query before `Take(1)`. Terminal capture validates and freezes the compiled read,
parameter values, map identity, provider/data source, and any caller resource binding without opening a connection or
executing SQL. Compilation and unsupported input can therefore fail synchronously at the terminal call.

Immutable scalar values are retained; byte arrays and supported one-dimensional scalar arrays are copied, preserving
their types, and copied again for each enumeration. Unknown mutable objects, custom/lazy collections, and unsupported
array shapes refuse with `FrozenParameterUnsupported` rather than being silently coerced or evaluated later. Values
already translated by an earlier `Where` call are captured as stored; closures are not reevaluated at execution.

Getting an enumerator is also cold. The first `MoveNextAsync` opens/executes. If its tokens and resources remain valid,
a fresh enumeration executes the same captured query against the database again, with fresh parameters and cursor state.
It does not replay cached rows or promise the same database snapshot. Concurrent caller mutation of the original query
during capture is unsupported; the query builder remains non-thread-safe.

## Observe early cleanup

Use the concrete `QueryStreamEnumerator<T>` returned by `GetAsyncEnumerator` when stopping early must establish cleanup
success. Its per-enumerator `Completion` task settles after all cleanup attempts and returns a result; it does not fault
or cancel. Await disposal before awaiting Completion, including when no advance occurred.

```csharp
var cursor = NoteOrm.Query(db).OrderBy(n => n.Title).StreamAsync(ct).GetAsyncEnumerator();
Exception? processingFailure = null;
QueryStreamCompletion? completion = null;
try
{
    while (await cursor.MoveNextAsync())
    {
        Console.WriteLine(cursor.Current.Title);
        break; // Deliberately process only the first row.
    }
}
catch (Exception error)
{
    processingFailure = error;
    throw;
}
finally
{
    await cursor.DisposeAsync();
    completion = await cursor.Completion;
    if (!completion.CleanupSucceeded && processingFailure is null)
        throw new InvalidOperationException("Stream cleanup failed.");
}
```

This host policy raises a safe cleanup error only when there is no active processing/advance exception to preserve.
If there is an active exception, retain the completion for protected inspection instead of replacing that exception or
running a throwing logger in `finally`. `PrimaryError` and `CleanupErrors` are sensitive inspection data: do not
format or serialize their original contents automatically. Completion describes the stream, not success of arbitrary
consumer-body work, which the stream cannot observe.

| Completion member | Meaning |
| --- | --- |
| `Reason` | `Completed`, `Disposed`, `Canceled`, or `Faulted` |
| `RowsYielded` | Number of rows already published to this consumer |
| `CleanupSucceeded` | Whether all attempted cleanup stages reported success |
| `PrimaryError` | Original internal failure, when present; sensitive |
| `CleanupErrors` | Bounded cleanup failures, in attempt order; sensitive |

A deliberate early stop normally ends as `Disposed`, not `Completed`. `CleanupSucceeded` alone says nothing about full
result consumption; also inspect `Reason` when deciding whether processing finished. A clean EOF establishes
`Completed`. If cleanup fails at EOF, the last `MoveNextAsync` throws `QueryStreamException` with `CleanupFailure` and
Completion is `Faulted`, even if all data rows were already yielded. Early `DisposeAsync` always remains nonthrowing
for cleanup errors so it cannot overwrite an unknown body exception.

## Cancellation, state, and partial results

You can supply a token at capture, at enumeration, or both. In this example, `requestToken` and `stopToken` are
application cancellation tokens. Either token cancels the enumeration; disposal also has a
private signal for its own in-flight operation and does not cancel your token sources.

```csharp
var sequence = NoteOrm.Query(db).StreamAsync(requestToken);
await foreach (var note in sequence.WithCancellation(stopToken))
{
    Console.WriteLine(note.Title);
}
```

Cancellation is passed through connection open, execute, and each asynchronous read, with checks around synchronous
mapping. It cannot interrupt arbitrary synchronous mapper code. Cancellation while paused is observed on the next
advance or explicit disposal; it does not start autonomous cleanup. If you stop advancing, dispose promptly.

`QueryStreamCanceledException` derives from `OperationCanceledException`. Other stream failures use
`QueryStreamException`, with `Error`, `RowsYielded`, and cleanup evidence. Parameter/shape/resource refusals can occur
before I/O. Safe category/counter summaries are available, but `InnerException`, `Completion.PrimaryError`, and cleanup
originals may contain SQL, values, or provider details. Prefer explicit category/counter fields over passing whole
exception objects to a logger or serializer.

Only one `MoveNextAsync` may be active on an enumerator. A second concurrent advance refuses with `ConcurrentAdvance`
without stealing the first read. `Current` is valid only after a successful advance and before the next advance or
disposal; a previously returned detached value remains usable afterward. After `Completed` or `Disposed`, advances
return `false`. After `Canceled` or `Faulted`, advances rethrow the cached failure; they do not resume or requery.
Repeated disposal is safe. Disposal racing an advance requests cancellation and awaits actual operation settlement;
a driver that ignores cancellation, or a synchronous mapper still running, can delay it. There is no hidden timeout
that abandons the operation.

A late read, mapping, cancellation, or EOF-cleanup failure cannot retract the prefix you already processed. Treat it as
partial work: the stream does not roll back consumer side effects, retry, replay, or repair them. Any recovery or
idempotent processing policy belongs to the application.

## Connections, transactions, and locks

| Query binding | Stream owns | Application owns |
| --- | --- | --- |
| Unbound query | Connection, command, reader | Context/source and authorization lifetime |
| `.On(connection, transaction)` | Command and reader | Caller handles, commit/rollback, disposal |
| `.On(unit)` | Command and reader on captured handles | The unit and its complete lifetime |

Unbound enumeration uses the ordinary gated open path and keeps its connection until cleanup. Independent unbound
enumerations have separate connections. Borrowed enumeration never reopens, replaces, commits, rolls back, or disposes
caller handles, and never closes the caller connection with the reader. An unlocked borrowed read may omit the
transaction; a requested row lock needs one. Connection/transaction mismatch and observable
committed/disposed-unit state refuse rather than fall back to another resource. A provider failure or cancellation may
leave a borrowed transaction aborted; the caller must decide how to roll it back or retire it.

Finish or await-dispose the stream before using another command on a borrowed handle, committing/disposing its unit,
or ending the context/authorization scope. A stream-only guard refuses a second live stream on the same borrowed
connection with `ConcurrentStream`. It does not police legacy buffered/native commands, unit commit/disposal, or
context disposal, and does not drain them. Those races remain caller lifetime errors with provider-dependent outcomes.
If reader/command cleanup is uncertain, later streams on that handle refuse with `ResourceUnavailable`; retire the
handle rather than expecting an automatic reconnect or repair.

Requested row locks require a valid captured caller transaction and native support for every required capability.
`ForUpdate` requires native `RowLock`; `ForNoKeyUpdate` additionally requires native `KeyPreservingRowLock`.
A nonnative requested lock that reaches stream capture refuses with a safe `InvalidRequest` wrapper and an
inspectable `CapabilityNotSupportedException`; ordinary builder capability checks can refuse earlier. An unrelated
capability does not disqualify `ForUpdate`. A valid lock query captured without a transaction refuses with
`ResourceMismatch`. PostgreSQL can use its native locks; SQLite's ordinary buffered lock emulation does not make a
stream lock native. Streaming adds no transaction, stronger isolation, snapshot, or lock-duration guarantee. Pausing a
cursor can continue to hold connection-pool capacity, locks, and a database snapshot until you finish or dispose it.

## Buffering and diagnostics boundaries

The ORM retains one `Current` row, transient one-row mapping workspace, finite lifetime/error state, and frozen query
input. It clears Current at the next advance or terminal/disposal and keeps no result history. This bound is independent
of result cardinality for a consumer that does not retain rows. It is not a process-memory bound: driver/network
buffers, database sort/hash/materialization, a large field, captured input arrays, and your own retained rows can still
be large. Streaming does not guarantee that the database or driver avoids its own buffering before the first row.

[SQL diagnostics](diagnostics.md) remain opt-in, with parameter values off by default and the existing redaction rules
when deliberately enabled. Streaming produces no per-row payload log or extra execute event on each advance.
`StatementEvent.Elapsed` measures command execution until the reader is returned, not total loop duration. A late
read/map failure can occur after a successful execute event; use the thrown failure and Completion for that outcome
instead of expecting a second statement event or treating command timing as consumption timing.
