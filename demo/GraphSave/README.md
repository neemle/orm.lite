# Save an explicit related graph

Run the `graph-save` chapter in the existing [demo project](../OrmLiteDemo.csproj). It uses generated repositories and
typed save tokens over projects, teams, tasks and Guid-key tags. The normal demo remains the default when no chapter
selector is supplied. Both providers use the same cases, models and assertions.

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
