# Explicit one-to-one database demo

One generated-model application runs the same cases with JIT or NativeAOT on PostgreSQL and SQLite.
It uses local packages built from this checkout.

## Prepare and run

From the repository root with .NET 10 SDK, first build the local packages using
[prepare-packages.sh](prepare-packages.sh). The public [project](OneToOneDemo.csproj) consumes those packages:

```sh
sh examples/one-to-one/prepare-packages.sh
```

### Run with JIT

```sh
export ONE_TO_ONE_POSTGRES='Host=localhost;Username=postgres;Database=postgres'
dotnet run --project examples/one-to-one/OneToOneDemo.csproj -c Release -- sqlite
dotnet run --project examples/one-to-one/OneToOneDemo.csproj -c Release -- postgres
```

### Choose the provider

Pass `sqlite` or `postgres` as the single application argument. PostgreSQL reads `ONE_TO_ONE_POSTGRES`;
use a disposable server. PostgreSQL needs permission to create/drop a database. Each run creates its own
`one_to_one_<GUID>` database and removes it in cleanup; the supplied database holds no demo tables. SQLite uses a temporary directory.
The legacy and provider-failure cases deliberately change tables only inside that owned fixture.

## NativeAOT

Install the native compiler toolchain. Select the runtime identifier for the execution machine;
these commands show Linux ARM64:

```sh
dotnet publish examples/one-to-one/OneToOneDemo.csproj -c Release -r linux-arm64 \
  -p:DemoNative=true -o artifacts/one-to-one/native10
artifacts/one-to-one/native10/OneToOneDemo sqlite
artifacts/one-to-one/native10/OneToOneDemo postgres
```

For .NET 11 use its SDK and a separate native output directory:

```sh
dotnet run --project examples/one-to-one/OneToOneDemo.csproj -c Release -p:DemoTfm=net11.0 -- sqlite
dotnet run --project examples/one-to-one/OneToOneDemo.csproj -c Release -p:DemoTfm=net11.0 -- postgres
dotnet publish examples/one-to-one/OneToOneDemo.csproj -c Release -r linux-arm64 \
  -p:DemoTfm=net11.0 -p:DemoNative=true -o artifacts/one-to-one/native11
artifacts/one-to-one/native11/OneToOneDemo sqlite
artifacts/one-to-one/native11/OneToOneDemo postgres
```

JIT and native binaries execute the same cases and print `ONE-TO-ONE-DEMO v1` followed by successful case results.
SQLite has an additional enabled-PRAGMA assertion; its assertion total is one higher than PostgreSQL.
This is an actual connection-enforcement check, not a PostgreSQL option or a substitute for DML.

## Declare one owner and inverse

[Profile.cs](Profile.cs) owns the physical nullable `PrincipalId` FK and explicitly names
[Person.Profile](Person.cs) as its inverse. The FK name, required `Restrict` action and unique index name
are declared separately. `Person` uses an integer key while `Profile` has its own Guid key: inverse matching
uses the principal/FK type rather than the dependent's own PK type.

The fresh baseline has one exact physical FK and a plain unfiltered unique index over only that FK column.
These indexes are separate from ordinary index reconciliation/hash. Multiple null FKs remain unrelated;
a principal is allowed to have no dependent. Properties are ordinary nullable navigation values:
inspection does not load or save anything.

## Load directions and absence

[DirectionCases.cs](DirectionCases.cs) loads a source through a generated full-entity reader and explicitly
awaits its generated connection or UnitOfWork loader. Each supported nonnull demand executes one statement,
including calculated target fields. Optional-null demand executes no lookup; a missing inverse returns null.
Caller resources remain caller-owned. [IntegrityCases.cs](IntegrityCases.cs) also proves valid writes,
orphan/second-dependent rejection, multiple nulls and atomic failed reassignment.

## Physical actions and visibility

[ActionCases.cs](ActionCases.cs), [ActionParent.cs](ActionParent.cs),
[RequiredDependent.cs](RequiredDependent.cs), [CascadeDependent.cs](CascadeDependent.cs) and
[SetNullDependent.cs](SetNullDependent.cs) demonstrate required FK loading and explicit physical actions.
Deleting a dependent does not delete its principal. `Cascade` is never inferred from ownership.

[VisibilityCases.cs](VisibilityCases.cs) distinguishes soft deletion from physical action. Owner-to-principal
loading retains the principal's live filter and may return absence without an unrestricted existence probe.
An inverse into a soft-delete/filtered dependent is explicitly unsupported and refuses before SQL.

## Provenance and failures

[GuardCases.cs](GuardCases.cs) and [AdditionalGuardCases.cs](AdditionalGuardCases.cs) show the conservative
read boundary. Exact registered generated full-row maps carry weak provenance. Any active mask, including
conditional masks, refuses traversal. New objects, custom/raw mapper calls and projections remain unknown.
The public read-only `OneToOneReadGuard.Require` checks/refuses; it never grants trust. Guards run before
cancellation handling, null-FK handling, SQL or navigation changes. A masked null FK cannot bypass the guard.

[LegacyCases.cs](LegacyCases.cs) deliberately removes the fixture's unique index and introduces a duplicate.
The inverse demand reads at most two rows, throws `OneToOneCardinalityException` and retains the old navigation.
Reading one visible row is not a guarantee about hidden physical rows: actual constraints provide integrity.

[CancellationCases.cs](CancellationCases.cs) proves cancellation without publication or resource takeover.
[FailureCases.cs](FailureCases.cs) uses the actual generated `Profile.Label` setter plus controlled reader
and command cleanup failures, checks that the original primary survives and both cleanups are attempted,
and separately checks cleanup-only refusal and a real missing-table provider error. The fault wrappers belong
to this demo; they add no public library fault-injection API or automatic error logging.

## Existing databases and unsupported shapes

[UpgradeCases.cs](UpgradeCases.cs) starts with an old baseline containing duplicate orphan rows, then adds
one-to-one metadata under the same token. No FK/index is retrofitted, data/history remain unchanged and
ordinary index reconciliation does not acquire the baseline-owned unique index.

[DeclarationCases.cs](DeclarationCases.cs) also refuses malformed manual metadata, constraint-name collisions
and an unmapped physical FK before SQL.

Existing databases need a reviewed explicit migration and orphan/duplicate validation. This feature performs
no rebuild, index repair or history rewrite. Unsupported mappings include ambiguous/dual ownership,
composite/alternate keys, unmanaged/view/cross-baseline endpoints and self/cyclic FK dependencies.
Masked/unknown traversal and filtered inverse targets remain explicit refusals. No hidden transaction,
retry, graph/eager/lazy traversal, cascade saving or orphan removal is promised.

## Feature guides

See the [one-to-one relation guide](../../docs/relations.md#6-declare-and-load-one-to-one-relations) for
declaration, query cost and refusal boundaries, and the
[migration guide](../../docs/migrations.md#one-to-one-integrity-in-fresh-and-existing-databases) for baseline ownership
and the explicit existing-database route.
