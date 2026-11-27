# Save an explicit related graph

Run the `graph-save` chapter in the existing [demo project](../OrmLiteDemo.csproj). It uses generated repositories and
typed save tokens over projects, teams, tasks and Guid-key tags. The normal demo remains the default when no chapter
selector is supplied. Both providers use the same cases, models and assertions.

The [entry point](GraphSaveDemo.cs), selected by [Program.cs](../Program.cs), runs every case below. See the
[relation guide](../../docs/relations.md#7-save-an-explicit-related-graph) for the API's intent, limit and outcome contract.

## Models and explicit intents

[GraphSaveProject](GraphSaveProject.cs) owns a nullable team reference, a task collection and a tag link collection.
[GraphSaveTeam](GraphSaveTeam.cs), [GraphSaveTask](GraphSaveTask.cs) and [GraphSaveTag](GraphSaveTag.cs) have
caller-supplied Guid keys. The [fixture](GraphSaveFixture.cs) populates those objects; setting a key or navigation
never selects a write operation.

Every reached related instance needs an explicit `GraphSaveIntents.With` entry. `Insert` requests a new row,
`Update` requires an existing row and writes the complete admitted physical payload, and `Unchanged` permits no
entity mutation. A key's default value does not choose among them. An enabled edge to an Unchanged anchor uses an
explicit rule, such as `Allow(GraphSaveProjectOrm.SaveRelations.Team, new(GraphSaveActions.None))` in the
[repeat case](GraphSaveRepeatCase.cs); `GraphSavePermissions.None` by itself enables no edge.

## Default-off

[GraphSaveDefaultCase](GraphSaveDefaultCase.cs) populates a reference, task collection and tag list, then saves with
`GraphSavePermissions.None`. Only the root is inserted. Related objects and their FK values remain untouched.

## Inserts and detached foreign keys

[GraphSaveInsertCase](GraphSaveInsertCase.cs) explicitly admits reference/collection inserts and link additions.
Each supplied instance has an explicit intent. The team, project, two tasks and one shared tag commit together with
correct physical FKs; duplicate occurrences of the same tag produce one exact join pair.
`BindInWriteBuffer` leaves caller properties unchanged. [GraphSaveFixture](GraphSaveFixture.cs) explicitly copies the
acknowledged result's FK bindings into its own objects before requesting subsequent updates.

## Unchanged anchors and repeat

[GraphSaveRepeatCase](GraphSaveRepeatCase.cs) uses `Unchanged` for existing team/tag anchors and `Update` for the root
and one task. Repeating this declared operation does not duplicate rows or the exact link pair. Omitted siblings and
links survive: omission never means deletion, unlinking, orphan removal or clearing an FK.

## Atomic rollback

[GraphSaveRollbackCase](GraphSaveRollbackCase.cs) requests a new project and two task inserts, ending with an existing
task's duplicate key. A scoped public Activity listener counts three graph-save command attempts: the first two
complete before the third fails. The acknowledged rollback leaves neither new prefix row committed, preserves the
existing task and leaves the input graph intact. It uses real database constraints, not a simulated successful save.

## Refusal and cancellation

[GraphSaveRefusalCase](GraphSaveRefusalCase.cs) checks insert-only permission against an Update target, a missing
Unchanged anchor, an unresolved physical FK under enabled database enforcement, a projected partial object and
pre-admission cancellation. The expected `NotStarted`/`RolledBack` outcomes leave no requested new root behind.
Unknown manual inputs are explicit host-authorized full-value payloads; read provenance is not authorization.

## Run with JIT or NativeAOT

From the repository root, pack the current packages as described in [the demo setup](../README.md), then run:

```sh
dotnet run --project demo/OrmLiteDemo.csproj -c Release -- graph-save sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' \
  dotnet run --project demo/OrmLiteDemo.csproj -c Release -- graph-save postgres
```

The PostgreSQL server must allow creation and removal of a scratch database. The reused
[GraphDemoDatabase](../Graph/GraphDemoDatabase.cs) creates a private database/file and removes it after the chapter;
never point this example at production. SQLite needs no external server.

With the NativeAOT prerequisites installed, publish and execute this same project:

```sh
dotnet publish demo/OrmLiteDemo.csproj -c Release -r linux-arm64 -p:PublishAot=true -o artifacts/graph-save-native
artifacts/graph-save-native/OrmLiteDemo graph-save sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' \
  artifacts/graph-save-native/OrmLiteDemo graph-save postgres
```

Use your target RID, such as `linux-x64`, for its matching machine. For .NET 11, use its SDK and
`-p:DemoTfm=net11.0`, with a separate output directory. `ORMLITE_EXPECT_DOTNET=10` or `11` checks the actual running
runtime. Every mode runs the same chapter; only the provider/runtime header varies. Exit zero and
`graph-save chapter ok; scratch removed` require every case and scratch cleanup to succeed.

For example, these are the .NET 11 counterparts, using its SDK and a separate native output:

```sh
ORMLITE_EXPECT_DOTNET=11 dotnet run --project demo/OrmLiteDemo.csproj -c Release -p:DemoTfm=net11.0 -- graph-save sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' ORMLITE_EXPECT_DOTNET=11 \
  dotnet run --project demo/OrmLiteDemo.csproj -c Release -p:DemoTfm=net11.0 -- graph-save postgres
dotnet publish demo/OrmLiteDemo.csproj -c Release -r linux-arm64 -p:DemoTfm=net11.0 -p:PublishAot=true -o artifacts/graph-save-native11
ORMLITE_EXPECT_DOTNET=11 artifacts/graph-save-native11/OrmLiteDemo graph-save sqlite
ORMLITE_GRAPH_POSTGRES='Host=localhost;Port=55432;Username=postgres;Database=postgres' ORMLITE_EXPECT_DOTNET=11 \
  artifacts/graph-save-native11/OrmLiteDemo graph-save postgres
```

The same public cases have executed successfully in all eight combinations: JIT/NativeAOT, PostgreSQL/SQLite and
.NET 10/11. The rollback and refusal assertions run in each combination. These commands select that same case;
publishing alone does not execute its checks.

## Limits and database work

The [request helper](GraphSaveChecks.cs) constructs `GraphSaveRequest<T>` with explicit root intent and a
`GraphSavePlan` containing related intents, permissions and all six positive finite limits:

| Group | Limit | Fixture value | What counts |
| --- | --- | --- | --- |
| Traversal | `MaxDepth` | 3 | Relation depth, with the root at zero. |
| Traversal | `MaxNodes` | 20 | Encountered object instances before persistent-identity deduplication. |
| Traversal | `MaxEdges` | 20 | Examined allowed relation slots, including duplicate list entries. |
| Work | `MaxEntityWrites` | 10 | Unique Insert/Update nodes. |
| Work | `MaxLinkAdds` | 10 | Unique proposed link pairs. |
| Work | `MaxStatements` | 30 | Planned entity writes, link additions and targeted anchor checks. |

These are fixture budgets, not production defaults. Choose limits for your supplied graph's fan-out and work.
The planner freezes admitted scalar values, collection membership and final FK buffers before opening the transaction;
an exceeded limit refuses with `NotStarted`, rather than committing a truncated prefix. Shared identities are written
once only when their intent and final buffers agree; conflicting payloads, FK ownership or mutation cycles refuse.

An Unchanged relation/link anchor uses a targeted key existence check on the same transaction; it does not fetch a
graph to discover children. Missing anchors and missing Update rows fail. Link additions deduplicate the exact pair;
other constraint conflicts still fail the transaction. A save therefore performs bounded multiple statements, not a
single joined write. Startup and transaction control traffic are separate from the planned-statement budget.

## Outcome and ownership boundaries

This chapter requests only `Owned` execution: one library-owned connection and transaction. `CallerJoined` and bare
caller-transaction participation are unsupported. Acknowledged precommit rollback is `RolledBack`; cancellation before
admission is `NotStarted`. Caller edits are never automatically reverted or tracked as persisted.

A lost commit response or unconfirmed rollback is `Unknown`: reconcile using domain keys before deciding whether to
retry. Never blindly repeat an Insert. An acknowledged commit followed by cleanup failure remains `Committed` and
exposes `CommittedResult`. This chapter does not simulate those transport/cleanup faults. Original provider errors
are sensitive programmatic diagnostics; the example prints safe case results instead of exception contents.

Traversal is finite, explicit and limited to supplied safe auto-properties/concrete Lists. No lazy graph discovery,
implicit upsert/delete/schema repair, hidden retry, stronger isolation or tenant/owner-policy bypass is promised.

The supported save shape is a generated managed, non-view model with one supported caller-supplied scalar key;
pure managed many-to-many links require Guid endpoint keys and the known exact pair key. Generated/composite keys,
custom or payload-bearing joins, unsafe getters/custom collections and unsupported policy-specialized shapes refuse.
Projected or masked inputs with known partial provenance also refuse before a transaction. Constructing or copying an
object does not establish authorization or completeness: the host must admit the context, keys and full payload.
Serialize caller edits during capture; later edits are not automatically reverted, tracked or reread by execution.
