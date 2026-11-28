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
| VACUUM | `VacuumAsync(VacuumRequest)` | Plain of the whole database (checkpoint first) | Plain and FULL, whole database or one table |
| ANALYZE | `AnalyzeAsync(AnalyzeRequest)` | whole database or one table | whole database or one table |
| REINDEX | `ReindexAsync(ReindexRequest)` | Blocking: database, table or index | Blocking and Concurrent: database, table or index |
| Integrity check | `CheckIntegrityAsync(IntegrityCheckRequest)` | Full, Quick and ForeignKeys, database or table | None, refused |

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
   is never reused, and the call does not consume Request-lane admission. Opening that connection spends the same
   budget: if the budget runs out while the connection is still opening, or the open fails with contention, the call
   returns `MaintenanceStatus.Busy` and releases the gate.
7. The gate and the connection are released on success, failure and cancellation.

The refusal in step 4 sees a unit of work only in the flow that began it. The library marks the flow when
`UnitOfWork.BeginAsync` is called, and that mark travels with the async flow into the methods it calls, not back out of
them. A unit of work begun inside an `async` helper, or inside `Task.Run`, and handed back to its caller therefore
leaves the caller's flow unmarked. A writing operation called there is not refused: it waits for the writer gate the
unit of work holds, for at most its budget, and then returns `MaintenanceStatus.Busy`. It never waits past the budget,
and the gate is free again once the unit of work ends. To keep the refusal, begin the unit of work in the flow that uses
it:

```csharp
// Refused with InvalidOperationException: the unit of work was begun in this flow.
await using var unit = await UnitOfWork.BeginAsync(db);
await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));

// Not refused: the unit of work was begun in another flow and returned. The call waits for the gate for its budget
// and returns Busy.
await using var handedBack = await Task.Run(() => UnitOfWork.BeginAsync(db));
var busy = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));
```

`Timeout` on a request is ONE budget for the whole call: every gate wait, every lock wait and every statement spend
from it, also when an operation runs more than one statement (VACUUM on SQLite checkpoints first). It defaults to the
provider's maintenance timeout: on SQLite that is the busy timeout (`OrmLite:BusyTimeoutSeconds`, 30 seconds by
default), on PostgreSQL `PostgresProvider.DefaultMaintenanceTimeout`, 30 seconds. Once the budget is spent, nothing more
starts: the call returns `MaintenanceStatus.Busy` before it would enter the gate or open a connection. The statement's
command timeout is what is left of the budget, rounded up to whole seconds (at least one). A long `VACUUM (FULL)` or blocking `REINDEX` of a big table
needs a longer `Timeout`; on PostgreSQL a longer budget also lets a queued lock request wait longer, and while it waits
for an `ACCESS EXCLUSIVE` or `SHARE` lock it blocks other sessions queued behind it.

On PostgreSQL the lock wait is bounded on the server: before the statement the maintenance connection runs
`SET lock_timeout = '<remaining>ms'` and after it `RESET lock_timeout`. A `REINDEX` of one index also runs, after the
`SET`, `SELECT n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = '<table>'::regclass`
to find the index's schema (see [Reindex](#reindex)). These session statements are not reported to diagnostics, so a
call still reports exactly one statement. `RESET lock_timeout` gets a command timeout of one
second; if it fails, the failure is ignored and the connection is disposed, and the driver's reset on close is the
second line. The command timeout gets one second of grace over the
budget so the server's lock timeout fires first and is reported as `Busy`. On SQLite the driver's busy retry is bounded
only by the command timeout and does not stop for a cancellation, so the library waits one second at a time: the
statement's command timeout is at most one second, and a busy or locked answer is retried on the same connection while
the budget lasts and the call is not cancelled. The retries stay one statement, with one diagnostics event whose
duration covers every attempt.

Cancellation wins over contention. Once the token passed to the call is cancelled, the call throws
`OperationCanceledException` carrying that token, also when the cancellation arrives during a lock wait, during the open
or when the result would have been `Busy`; the original exception, if any, is its inner exception. On SQLite a
cancellation during a lock wait is seen within about one second.

What counts as contention, reported as `Busy`:

| Provider | Contention |
| --- | --- |
| SQLite | the writer gate was not free within the budget; `SQLITE_BUSY` (5) or `SQLITE_LOCKED` (6), extended codes included |
| PostgreSQL | SQLSTATE `55P03` (`lock_not_available`), raised when the `lock_timeout` above runs out |

A command timeout during a long statement is not contention: it propagates as the driver's exception.

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
| A prerequisite step did not complete, so the statement was not issued | `Status = NotRun` |
| A check ran and found problems | `Status = IssuesFound`, with the findings |
| The calling flow holds the writer gate | `InvalidOperationException`, before any SQL |
| The call was cancelled | `OperationCanceledException`, never `Busy` |
| The provider lacks the operation or option | `CapabilityNotSupportedException`, before any connection |
| Any other database failure | the original `DbException`, unwrapped |

If you want an incomplete result to be an exception, call `result.ThrowIfNotComplete()`: it throws
`MaintenanceIncompleteException`, whose `Result` is the result itself, unless `Status` is `Completed`.

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

## VACUUM

`VacuumAsync` reclaims the space of deleted rows. `VacuumMode` is required: `Plain` or `Full`. `Plain` is never turned
into `FULL`.

| | SQLite | PostgreSQL |
| --- | --- | --- |
| `Plain`, whole database | `VACUUM`, after a WAL checkpoint (below) | `VACUUM` |
| `Plain`, one table (`Table = NoteOrm.Descriptor`) | refused: `Capability.VacuumTable` | `VACUUM <table>` |
| `Full` | refused: `Capability.VacuumFull` | `VACUUM (FULL)` or `VACUUM (FULL) <table>` |

The table is named only by its generated descriptor and quoted by the provider, exactly as every other statement names
it. A view descriptor is refused with `ArgumentException` before the capability check. A refused option throws `CapabilityNotSupportedException` before any connection opens; the operation's own
capability (`Capability.Vacuum`) is checked first.

**SQLite: checkpoint, then VACUUM.** In WAL mode VACUUM writes the rebuilt database through the log, so `VacuumAsync`
first runs `CheckpointWalAsync` in `Truncate` mode, then:

- If the checkpoint is `Completed`, or `NotWal` (a rollback-journal or in-memory database has nothing to checkpoint),
  it runs `VACUUM`.
- If the checkpoint is `Partial` or `Busy` (a reader still needs frames), VACUUM is not issued: the result is
  `NotRun`, and `Checkpoint` carries the checkpoint result.
- If the writer gate was not free for the checkpoint, the result is `Busy` and `Checkpoint` is null.
- If the checkpoint completed but spent the whole budget, VACUUM is not issued: the result is `Busy`, and `Checkpoint`
  carries the checkpoint result.

Each step enters the writer gate for itself and releases it after, so another writer may run between the checkpoint and
VACUUM. Both steps share the one budget, and each reports its own diagnostics statement (`WalCheckpoint`, then
`Vacuum`). After VACUUM the main file is smaller but the `-wal` file may stay large until the next checkpoint:
`VacuumAsync` does not checkpoint again, so call `CheckpointWalAsync(new(WalCheckpointMode.Truncate))` afterwards if you
want the log truncated.

**PostgreSQL.** `Plain` marks dead rows reusable without blocking reads or writes. `Full` rewrites the table into a new
file, returns the freed space to the operating system, and holds an `ACCESS EXCLUSIVE` lock on the table for the whole
run, so every read and write of that table waits. VACUUM cannot run inside a transaction block; the library runs it on
its own connection, never in a unit of work.

On both providers `VacuumAsync` from a flow with an open `UnitOfWork` on the same database throws
`InvalidOperationException` before any SQL runs.

`VacuumResult` carries `Mode`, `Table` (the table name, or null), `Status` and `Checkpoint`.

```csharp
var vacuum = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Timeout = TimeSpan.FromMinutes(2) });
if (vacuum.Status == MaintenanceStatus.NotRun)
    Console.WriteLine($"a reader held the log (checkpoint {vacuum.Checkpoint?.Status}); VACUUM was not run");
vacuum.ThrowIfNotComplete();
```

## Analyze

`AnalyzeAsync` refreshes the statistics the query planner uses. `AnalyzeScope` is required:

```csharp
await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database));
await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Table) { Table = NoteOrm.Descriptor });
```

| Scope | SQLite | PostgreSQL |
| --- | --- | --- |
| `Database` | `ANALYZE main`: every table and index of the main schema | `ANALYZE`: every table the role may analyze |
| `Table` | `ANALYZE main.<table>` | `ANALYZE <table>` |

`Table` takes a generated descriptor; it must be set for `Table` and null for `Database`, and a view descriptor is
refused (`ArgumentException`), all before any connection opens. There is no index scope (PostgreSQL has no ANALYZE of an
index), and `PRAGMA optimize`, `analysis_limit`, column lists, `VERBOSE` and `SKIP_LOCKED` are not offered. ANALYZE
never changes data. Both shipped providers support it (`Capability.Analyze` is `Native`); another provider is refused
by name.

**Locks.** SQLite's ANALYZE writes `sqlite_stat1`, so it takes the writer gate, like a write: an ORM write on the same
database waits for it, and a call from a flow with an open `UnitOfWork` on the same database throws
`InvalidOperationException` before any SQL runs. PostgreSQL takes no in-process gate and is allowed inside a unit of
work's flow; ANALYZE takes `SHARE UPDATE EXCLUSIVE` on each table, which does not block reads or writes but waits for a
lock such as `ACCESS EXCLUSIVE`; that wait is bounded by the budget through `lock_timeout`.

**Busy.** Contention returns `Busy`: on SQLite the writer gate or a database lock (another connection's
`BEGIN IMMEDIATE`, for example) that does not clear within the budget; on PostgreSQL a lock wait that reaches the
budget (SQLSTATE `55P03`). A table that does not exist propagates the database's own error (SQLite `no such table`,
PostgreSQL `42P01`).

**Privileges on PostgreSQL.** `ANALYZE` of the whole database skips, with a server `WARNING`, every table the role may
not analyze; that is not an error, so the result is still `Completed`. Run it as the table owner (or a role with the
`MAINTAIN` privilege) when every table must be analyzed.

`AnalyzeResult` carries `Scope`, `Table` (the table name, or null) and `Status`.

A runnable example:

```csharp
var analyzed = await db.Maintenance.AnalyzeAsync(
    new AnalyzeRequest(AnalyzeScope.Table) { Table = NoteOrm.Descriptor, Timeout = TimeSpan.FromSeconds(10) });
Console.WriteLine(analyzed.IsComplete ? "statistics refreshed" : "busy, try again later");
```

## Reindex

`ReindexAsync` rebuilds indexes. Both the scope and the mode are required:

```csharp
await db.Maintenance.ReindexAsync(new ReindexRequest(ReindexScope.Database, ReindexMode.Blocking));
await db.Maintenance.ReindexAsync(new ReindexRequest(ReindexScope.Table, ReindexMode.Concurrent) { Table = NoteOrm.Descriptor });
await db.Maintenance.ReindexAsync(new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking)
{
    Table = NoteOrm.Descriptor,
    Index = NoteOrm.Descriptor.Indexes[0],
});
```

| Scope | Targets | SQLite | PostgreSQL |
| --- | --- | --- | --- |
| `Database` | none | `REINDEX` | `REINDEX DATABASE [CONCURRENTLY]` (no name: system catalogs are skipped) |
| `Table` | `Table` | `REINDEX main.<table>` | `REINDEX TABLE [CONCURRENTLY] <table>` |
| `Index` | `Table` and one of its `Indexes` | `REINDEX main.<index>` | `REINDEX INDEX [CONCURRENTLY] <schema>.<index>` |

| Mode | SQLite | PostgreSQL |
| --- | --- | --- |
| `Blocking` | Native (`Capability.Reindex`) | Native (`Capability.Reindex`) |
| `Concurrent` | None: refused with `Capability.ReindexConcurrently`, never downgraded | Native (`Capability.ReindexConcurrently`) |

The targets are generated descriptors, quoted by the provider. `Table` scope reaches the implicit primary-key and
unique indexes, which have no descriptor; `Index` scope takes an `IndexDescriptor` that belongs to the table's
`Indexes`. A target that does not fit the scope, an index of another table and a view are refused with
`ArgumentException` before any connection opens.

On PostgreSQL an index always lives in its table's schema, but a bare index name is resolved through `search_path` and
can name another table's index. So for `Index` scope the maintenance connection first asks for the table's schema, after
the lock guard and resolving the table exactly as every other statement names it, and the REINDEX names the index in
that schema. A table that no longer exists raises `42P01`, and so does an index missing from that schema; both propagate.
SQLite index names are unique per schema, so SQLite needs no lookup.

**Locks per mode.**

- SQLite: REINDEX needs the write lock, so it takes the writer gate. ORM writes on the same database wait for it, and a
  call from a flow with an open `UnitOfWork` on the same database throws `InvalidOperationException` before any SQL.
- PostgreSQL `Blocking`: a `SHARE` lock on the table, which blocks writes but not reads, and an `ACCESS EXCLUSIVE` lock on
  each index being rebuilt, which blocks reads that would use it.
- PostgreSQL `Concurrent`: reads and writes continue; the rebuild waits for transactions older than it to finish. Like
  `DATABASE`, it cannot run inside a transaction block; the library runs every REINDEX on its own connection.

PostgreSQL takes no in-process gate and is not refused inside a unit of work's flow; its lock waits are bounded by the
budget through `lock_timeout`, and a wait that reaches the budget (SQLSTATE `55P03`) returns `Busy`. On SQLite a busy
gate or database lock (another connection's `BEGIN IMMEDIATE`) returns `Busy`. Any other failure propagates as the
database's own error: a table or index that no longer exists, a missing privilege (`42501`), or a unique violation a
rebuild exposes.

**Leftover `_ccnew` indexes.** A `Concurrent` rebuild that fails, is cancelled or returns `Busy` (the `lock_timeout` also
bounds its waits for older transactions) can leave an invalid index named `<index>_ccnew` behind. The library never
drops an index on its own and does not report the leftover. Find and drop leftovers yourself:

```sql
SELECT c.relname FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE NOT i.indisvalid;
DROP INDEX CONCURRENTLY IF EXISTS "ix_notes_title_ccnew";
```

REINDEX does not refresh planner statistics; call `AnalyzeAsync` afterwards when you want them fresh (for example for
expression indexes).

`ReindexResult` carries `Scope`, `Mode`, `Table`, `Index` (names, or null) and `Status`. Table data is never changed.

## Integrity checks

`CheckIntegrityAsync` asks the database whether its file and its data are sound, and returns what it finds as data.
`IntegrityCheckMode` is required.

| Mode | SQLite statement | What it detects | Relative cost |
| --- | --- | --- | --- |
| `Full` | `PRAGMA integrity_check` | the file structure, every index against its table rows, `NOT NULL` and `CHECK` constraints | highest: reads every table and index |
| `Quick` | `PRAGMA quick_check` | the file structure, without comparing indexes with their tables | lower: about linear in the file size |
| `ForeignKeys` | `PRAGMA foreign_key_check` | rows whose declared foreign key references a missing parent row | proportional to the rows with foreign keys |

Foreign-key violations are reported ONLY by `ForeignKeys`: `Full` and `Quick` return `Completed` on a database whose
only problem is a dangling foreign key. Violations arise when rows are written with `PRAGMA foreign_keys=OFF` (SQLite's
default for a connection that does not turn it on), by another client or tool that does not enforce them, or by a
schema change that added the key after the rows.

`Table = NoteOrm.Descriptor` limits any mode to one table; the name is quoted by the provider. Table-scoped `Full` and
`Quick` need SQLite 3.33 or later. The SQLite package ships a bundled engine far newer than that; if you swap the
SQLitePCL provider for an older system SQLite, the engine's own error propagates.

`IntegrityCheckResult` carries `Mode`, `Status`, `Findings` and `Truncated`:

- `Completed`: no findings; `IsComplete` is true.
- `IssuesFound`: at least one finding; `IsComplete` is false. Each `IntegrityFinding` has `Kind` (the mode) and a
  `Message` that is never null. For `Full` and `Quick` the message is SQLite's text, verbatim. For `ForeignKeys` the
  finding also carries `Table`, `RowId` (null for a `WITHOUT ROWID` table), `ReferencedTable` and `ForeignKeyId`, and the
  message reads `<table> row <rowid> references a missing row in <parent> (foreign key <fkid>)`, without `row <rowid>`
  when there is no rowid.
- `Busy`: the database was locked (`SQLITE_BUSY` or `SQLITE_LOCKED`) for longer than the budget, for example by another
  connection's `BEGIN EXCLUSIVE`.

`MaxFindings` (default 100, from 1 to `IntegrityCheckRequest.MaxFindingsLimit`, 10,000; anything else throws
`ArgumentOutOfRangeException`) bounds the findings returned. The whole-database `Full` and `Quick` checks ask SQLite for
`MaxFindings + 1` messages, so the engine stops early; every other form is read for at most `MaxFindings + 1` rows.
`Truncated` is true when there were more than `MaxFindings`.

Problems found are data, `IssuesFound`; a file the engine cannot read at all (not a database, `SQLITE_NOTADB`) throws
the original `DbException`. The check is read-only and never changes the file. It takes no writer gate, so it runs
inside the flow of an open `UnitOfWork` too, on its own connection: it sees committed data only and does not wait for
the unit of work.

PostgreSQL is refused (`Capability.IntegrityCheck` is `None`). For PostgreSQL, use the `amcheck` extension or the
`pg_amcheck` tool.

A runnable example:

```csharp
var check = await db.Maintenance.CheckIntegrityAsync(
    new IntegrityCheckRequest(IntegrityCheckMode.ForeignKeys) { MaxFindings = 20 });
foreach (var finding in check.Findings) Console.WriteLine(finding.Message);
if (check.Truncated) Console.WriteLine("more than 20 violations; narrow the check with Table");
```
