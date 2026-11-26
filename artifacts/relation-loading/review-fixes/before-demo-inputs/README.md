# Relation loading demo

Run the same executable scenarios with JIT or NativeAOT, on SQLite or PostgreSQL. The demo uses the
runtime and generator in this checkout. It exercises generated typed navigation paths, eager reads,
explicit asynchronous lazy demands and caller-owned transactions without getter I/O.

## Run

Use .NET 10 SDK; NativeAOT also needs the platform's native compiler toolchain. PostgreSQL examples
need a disposable database: the fixture resets its five `policy_*` tables. SQLite creates and deletes
its own temporary database. No migrations or application tables are modified.

From the repository root:

```sh
export ORMLITE_EXPECT_DOTNET=10
export ORMLITE_TEST_POSTGRES='Host=localhost;Username=postgres;Database=relation_loading_demo'

dotnet run --project examples/relation-loading/RelationLoadingDemo.csproj -c Release -- sqlite
dotnet run --project examples/relation-loading/RelationLoadingDemo.csproj -c Release -- postgres
```

Publish the same sources as a native executable. This example targets Linux ARM64; choose the RID
matching the machine where the executable will run.

```sh
dotnet publish examples/relation-loading/RelationLoadingDemo.csproj -c Release \
  -r linux-arm64 -p:PolicySmokeNative=true -o /tmp/relation-loading-demo
/tmp/relation-loading-demo/PolicySmoke sqlite
/tmp/relation-loading-demo/PolicySmoke postgres
```

Each run ends with `policy smoke <provider> assertions=109 PASS` when the runtime assertion above is
enabled. Any failed scenario throws and exits unsuccessfully. The provider argument changes only the
fixture; [Program.cs](Program.cs) invokes the same cases in every mode. The project can target .NET 11
with `-p:PolicySmokeTfm=net11.0`; set `ORMLITE_EXPECT_DOTNET=11` for that run.

## Eager reads and suppression

[ReadCases.cs](ReadCases.cs) loads a root page with eager books and back references, checks shared
identity through a cycle, and counts batched statements. It compares this with per-call eager
suppression, scalar reads and an entity without relation policies. Model declarations are in
[Author.cs](Author.cs) and [Book.cs](Book.cs); all six finite graph bounds are explicit.

## Explicit lazy demand

[LazyCases.cs](LazyCases.cs) reads a suppressed root set into a retained scope, inspects unloaded and
empty states, calls `scope.LoadAsync(author, AuthorOrm.Paths.Tags)`, and checks successful caching.
Getter access and an actual statically typed JSON serialization perform no relation I/O. Detaching
retains loaded values and refuses later demands before SQL.

## Cancellation, faults and bounded work

[ConcurrencyCases.cs](ConcurrencyCases.cs) pauses real reader work to show joined demands, independent
waiter cancellation, initiating cancellation, explicit retry and detach during I/O.
[FaultCases.cs](FaultCases.cs) checks mapper failures and cached faults without partial publication.
[QueueCases.cs](QueueCases.cs), [LimitCases.cs](LimitCases.cs) and [LifetimeCases.cs](LifetimeCases.cs)
exercise finite queues, all six graph budgets, expiry and cancellation/draining on scope, context or
unit-of-work shutdown. No demand retries implicitly.

## Transactions and connection origins

[BindingCases.cs](BindingCases.cs) uses a generated keyed read with the exact unit-of-work connection
and transaction. It checks that explicit `.On(connection)` with a missing transaction is refused,
and that a committed unit cannot be accepted as a generic bare connection.
[GateOriginCases.cs](GateOriginCases.cs) checks ordinary externally opened gated connections, shared
gate/provider objects with distinct selected contexts, mismatched providers and stale leases.
[CustomOriginCases.cs](CustomOriginCases.cs) shows known custom-source Open/UoW origins and safe
refusal of unobserved externally opened custom connections, without probe opens.

## Visibility and captured keys

[VisibilityCases.cs](VisibilityCases.cs) covers root masking, masked navigation keys, nullable
references and key changes after attachment. Refused work adds no SQL statements.

The [relation guide](../../docs/relations.md) describes the broader graph and loader surface. This
demo makes no release or package-version claim and does not implement save cascades or getter I/O.
