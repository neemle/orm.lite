# Database maintenance

[Documentation](README.md)

`OrmContext.Maintenance` runs explicit maintenance on the context's database, so you no longer send raw `PRAGMA` or
maintenance SQL. Each operation is one method that takes a request whose mode (or scope) you must name; there is no
default mode and no overload that takes SQL text. Nothing runs on its own: the library never checkpoints, vacuums or
analyzes in the background.

```csharp
await using var db = await OrmLite.OpenAsync(options);
var result = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));
```

| Operation | Method | SQLite | PostgreSQL |
| --- | --- | --- | --- |
| WAL checkpoint | `CheckpointWalAsync(WalCheckpointRequest)` | Native | None, refused |

An operation the provider does not support is refused with `CapabilityNotSupportedException`, naming the provider, the
method and the capability, before any connection opens. A provider that does not implement `IMaintenanceProvider` is
refused the same way. The capabilities are listed in [the capability matrix](providers.md#capability-matrix).

## Concurrency, lanes and timeouts

Every operation runs in the same order:

1. The request is validated. A null request throws `ArgumentNullException`; an undefined mode (including `0`) or a
   `Timeout` of zero or less throws `ArgumentOutOfRangeException`.
2. A cancelled token throws `OperationCanceledException`.
3. The capability is checked, as above.
4. An operation that writes refuses to run from a flow that holds an open `UnitOfWork` on the same database: it would
   wait for a writer gate that the flow itself holds. It throws `InvalidOperationException` before any SQL runs.
   Commit or dispose the unit of work first. A unit of work in another flow, or on another database, does not count.
5. An operation that writes enters the database's in-process writer gate, the same one an ORM write and a
   `UnitOfWork` take, so it is never a second lock. If the gate is not free within the budget the call returns
   `MaintenanceStatus.Busy` without opening a connection.
6. The statement runs on its own connection from the Ops lane, outside any transaction. A unit of work's connection
   is never reused, and the call does not consume Request-lane admission.
7. The gate and the connection are released on success, failure and cancellation.

`Timeout` on a request is the budget of the whole call, gate wait and statement included. It defaults to the
provider's maintenance timeout: on SQLite that is the busy timeout (`OrmLite:BusyTimeoutSeconds`, 30 seconds by
default). The statement's command timeout is what is left of the budget, rounded up to whole seconds.

While a writing operation holds the gate, ORM writes on the same database wait for it and then complete, the same as
they wait for a unit of work. Writes to another database file are not delayed.

Each call reports one statement to the diagnostics sink: a `StatementEvent` whose `Kind` is
`StatementKind.Maintenance` and whose `Operation` names the `MaintenanceOperation`. The event carries the statement
text the provider rendered and never the connection string. The OpenTelemetry activity carries the same name in the
`orm.operation` tag.

### Results and errors

Every result implements `IMaintenanceResult`: `Operation`, `Provider`, `Status` and `IsComplete`, which is exactly
`Status == MaintenanceStatus.Completed`.

| Outcome | How it is reported |
| --- | --- |
| The work was done | `Status = Completed` |
| Contention that did not clear within the budget | `Status = Busy`, never an exception |
| The calling flow holds the writer gate | `InvalidOperationException`, before any SQL |
| The provider lacks the operation or option | `CapabilityNotSupportedException`, before any connection |
| Any other database failure | the original `DbException`, unwrapped |

## WAL checkpoint

`CheckpointWalAsync` copies the frames of a SQLite write-ahead log back into the database file. A database opened with
`OrmLite.OpenAsync` on a file is in WAL mode; SQLite also checkpoints on its own once the log passes
`wal_autocheckpoint` pages, and this method does not change that setting or the journal mode.

| Mode | What SQLite does | Writer gate |
| --- | --- | --- |
| `Passive` | checkpoints as many frames as it can without waiting for readers or writers | no, it never waits |
| `Full` | waits for writers, then checkpoints every frame no reader still needs | yes |
| `Restart` | as `Full`, then waits for readers so the next writer starts the log from the beginning | yes |
| `Truncate` | as `Restart`, then truncates the `-wal` file to 0 bytes | yes |

The mode you name is the mode SQLite runs: it is never raised or lowered.

`WalCheckpointResult` carries `Mode`, `Status`, `WalFrames` and `CheckpointedFrames`, the last two verbatim from
SQLite's `busy`, `log` and `checkpointed` answer:

| Status | Meaning |
| --- | --- |
| `Completed` | no contention, and every frame of the log was checkpointed; after `Truncate` the `-wal` file is 0 bytes |
| `Partial` | no contention, but frames remain, because a reader still needs them; typical for `Passive` while a reader is open |
| `Busy` | SQLite reported busy, or the writer gate was not free within the budget (then both frame counts are null) |
| `NotWal` | the database is not in WAL mode (a rollback journal or an in-memory database), so there was nothing to do; both counts are null |

A reader that holds an open read snapshot keeps the frames it reads. While it is open, `Passive` returns `Partial`
without waiting and `Truncate` returns `Busy`; the reader's results do not change. Once the reader ends, `Truncate`
returns `Completed`.

Cancelling the token while the call waits for the gate or runs throws `OperationCanceledException` and releases the
gate and the connection, so the next write and checkpoint proceed.

PostgreSQL is refused: `Capability.WalCheckpoint` is `None` there. PostgreSQL's `CHECKPOINT` is a different operation
(it flushes dirty buffers, does not shorten the WAL, and needs superuser or `pg_checkpoint`), so the library does not
map one to the other.

A runnable example, on a file database:

```csharp
using Neemle.OrmLite;

var options = new OrmLiteOptions { Provider = "sqlite", Path = "app.db" };
await using var db = await OrmLite.OpenAsync(options);

var checkpoint = await db.Maintenance.CheckpointWalAsync(
    new WalCheckpointRequest(WalCheckpointMode.Truncate) { Timeout = TimeSpan.FromSeconds(5) });

switch (checkpoint.Status)
{
    case MaintenanceStatus.Completed:
        Console.WriteLine($"checkpointed {checkpoint.CheckpointedFrames} of {checkpoint.WalFrames} frames");
        break;
    case MaintenanceStatus.Busy:
        Console.WriteLine("a reader or writer held the log; try again later");
        break;
    case MaintenanceStatus.NotWal:
        Console.WriteLine("not a WAL database; nothing to checkpoint");
        break;
}
```
