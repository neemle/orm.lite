# Build, test, and package

[Documentation](README.md)

## Build, test, pack, demo

Plain dotnet commands, from a fresh clone, with the .NET 10 SDK or a later one:

    dotnet build Neemle.OrmLite.slnx -c Release       # the library and its tests
    dotnet test Neemle.OrmLite.slnx -c Release        # the tests; no database needed
    dotnet pack Neemle.OrmLite.slnx -c Release -o artifacts -m:1   # the three packages
    dotnet run --project demo                          # the demo on SQLite, a consumer of the packages in ./artifacts

The demo needs no database server. To run it on PostgreSQL, start the throwaway one and override the provider block
(see the [demo guide](../demo/README.md#the-same-demo-on-postgresql)), the settings being the `OrmLite` section:

    docker compose -f demo/compose.yaml up -d --wait   # a throwaway PostgreSQL on 127.0.0.1:55432
    docker compose -f demo/compose.yaml down -v        # stop and remove it, and any volume it created, when done

The demo restores the packages from `./artifacts` into `artifacts/.demo-packages`; the demo's build notices that the
packed files changed and drops its restored copy of the three packages before restoring, so packing the same version
again and running the demo always uses the new build.

The behavioural suites (queries, migrations, JSON documents, row history, trash, restore and revert) run on SQLite always
and on PostgreSQL when `ORMLITE_TEST_POSTGRES` holds the connection string of a server that may create databases (for
example `Host=127.0.0.1;Username=postgres`); each run creates and drops a database of its own.

The whole suite is memory-hungry: measured in a container, one test process running every test with
`xUnit.MaxParallelThreads=1` peaked at about 4.2 GiB on a quiet machine and at 4.98 GiB under a 5 GiB cap, and the
default parallel run was killed at 3 GiB and at 6 GiB. A run that ends with "Test host process crashed" and no failing
test has been seen under host load and may be the same kill (check `docker inspect --format '{{.State.OOMKilled}}'` and
exit code 137; it was not confirmed for those runs), so give the test container well over 5 GiB, pass
`-- xUnit.MaxParallelThreads=1` (`scripts/StepShape.cs` fails when a CI test step drops it), or run the suite in
`--filter` chunks. `--blame-crash` (CI passes it) records which test was running when the host died. The cause of the
size is not proven. The strongest suspect is that seven generator test helpers rebuilt about 150 metadata references and
a compilation on every call; they now share one list (`GeneratorReferences`). The consolidated run measures the peak
memory (`docker stats` of the test container, and the TRX) with that change; until then it is a suspect, not a finding.

A plain local `dotnet test` stays green with the PostgreSQL classes skipped; only the skip gate turns a skip into a
failure. The CI test job runs twice, once per .NET runtime (`10.0.x` and `11.0.x`, each leg on its own runner and its
own throwaway server, `fail-fast: false`): the runtime can change what a test observes (the conversion of a `double` to
a `decimal` differs between them). The test project targets net10.0, so on the .NET 11 leg the test host rolls forward
to the installed .NET 11 runtime (`DOTNET_ROLL_FORWARD=LatestMajor`; the .NET 10 leg names the default `Minor`), and
`TestHostRuntimeTests` fails a leg whose host runs on another major than `ORMLITE_EXPECT_DOTNET` names. The shipped
libraries stay net10.0. The `aot-smoke` job publishes the NativeAOT consumer on both runtimes too (its target framework
follows the leg: `-p:AotSmokeTfm=net10.0` or `net11.0`) and compares the runtime the binary prints with the leg;
`scripts/MatrixShape.cs` fails when either job loses a leg or this wiring. Each leg uploads its own
`test-results-<version>` artifact. The CI test job starts `tests/compose.yaml` (the `neemle/pg.lite:19` image) with
`docker compose up --wait`, sets `ORMLITE_TEST_POSTGRES` for the test step, runs `dotnet test` with the TRX logger and
one test thread (`xUnit.MaxParallelThreads=1`; the default parallel run was killed for memory in a capped container),
and then runs `dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx tests/skip-allowlist.txt
--require-postgres tests/skip-witness.txt`. The gate fails on a skipped test that is not on
`tests/skip-allowlist.txt` with a reason (the tests skipped on PostgreSQL by design), on a listed test that did not
skip, on a malformed or inconsistent TRX, and on a class of `tests/skip-witness.txt` (hand-written, server-only
classes) that is absent or has no passed result. `dotnet run scripts/WorkflowShape.cs -- check` fails when an edit
removes the compose step, the variable, the logger or the gate step from the test job, or loosens `tests/compose.yaml`
or `demo/compose.yaml` (the image tag, the exec-form health check, the loopback ports, the tmpfs); both tools have a
`self-test` that CI runs first (in stage 1, before any test runs). The same run locally, against the throwaway server of
`tests/compose.yaml` (image `ORMLITE_TEST_PG_IMAGE`, default `neemle/pg.lite:19`; host port `ORMLITE_TEST_PG_PORT`,
default 55433; the file's header lists the other variables for the image's own port, role and data path; the demo's
own throwaway server is `demo/compose.yaml`, on the same image):

    docker compose -f tests/compose.yaml up -d --wait
    ORMLITE_TEST_POSTGRES='Host=127.0.0.1;Port=55433;Username=postgres' dotnet test tests/Tests -c Release \
      --logger "trx;LogFileName=tests.trx" --results-directory TestResults -- xUnit.MaxParallelThreads=1
    dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx tests/skip-allowlist.txt \
      --require-postgres tests/skip-witness.txt
    docker compose -f tests/compose.yaml down

The test job then runs the data-literal tests once more under a non-UTC time zone (`TZ=Pacific/Auckland`, the
filter `FullyQualifiedName~DataLiteral&FullyQualifiedName!~Postgres`, its own TRX `TestResults/zone.trx`) and gates that
TRX with `tests/zone-allowlist.txt` (empty: nothing may skip) and `tests/zone-witness.txt` (the classes that must
pass). A `DateTimeKind.Local` moment is converted to UTC when it is written as a literal or bound, and the default
UTC runner cannot tell that conversion from none; `scripts/StepShape.cs` fails when the step or its gate goes.
The same run locally (needs the zone database; without it the zone falls back to UTC and `LocalZoneInForceTests`
fails):

    TZ=Pacific/Auckland dotnet test tests/Tests -c Release \
      --filter "FullyQualifiedName~DataLiteral&FullyQualifiedName!~Postgres"

The core is also run on a third provider that is not PostgreSQL and not SQLite. `tests/Ports.Tests`
references the Runtime and Sql projects only (no provider package, no driver) and holds a fake provider built from the
public ports: a serializer, a capability set and an actor stamp (it supplies no migration planner, so the core's
lock-free default runs), over an ADO.NET source that returns canned rows. Its tests run the migration runner and a
query on it. The test job runs the project in a step of
its own with its own TRX (`TestResults/ports.trx`, a TRX logger overwrites a file of the same name, so each test project
has its own step), and gates it with `tests/ports-allowlist.txt` (empty) and `tests/ports-witness.txt`. The
neutrality ratchet, `CoreNeutralityTests` of the main test project, scans the Runtime and Sql sources for provider
types, ids and SQL text and fails on any that `tests/Tests/Ports/core-coupling-allowlist.txt` does not
record; that list is empty and a line is never added to it. The same test freezes the project references. The
same run locally:

    dotnet test tests/Ports.Tests -c Release

CI (`.github/workflows/ci.yml`) then runs `dotnet run scripts/PackCheck.cs -- package artifacts`
(the entries, versions and dependencies of the three packages and the throwaway-consumer matrix: default reference,
runtime-only, analyzer-only, default tenant, actor setting, downstream project, central package management,
`PrivateAssets=all`, a lock file, and consumers of only the Sqlite
package, only the Postgres package, both and neither, which also prove the registration, the default provider list,
the opt-out and an application over a data library), `-- demo-guard` (the demo must reference the packages at the packed
version, never a project or an assembly, and carry no `nuget.config` or `global.json` of its own) and
`-- demo-guard-self-test` (the guard's own bypass cases). Every consumer restores Neemle.CSharpComposer, and the
provider consumers Npgsql and Microsoft.Data.Sqlite, from the package cache (`ORMLITE_PACK_PACKAGES`,
`NUGET_PACKAGES` or the user cache), so restore the solution first. CI also packs again in two demo jobs and runs the
demo on SQLite (no service) and against a PostgreSQL started from `demo/compose.yaml` with `docker compose up --wait`,
requiring the `demo ok` line from each, the soft delete, restore and history chapter included (see demo/README.md).
CI packs into the workspace only; nothing is published.

The CI jobs run in four ordered stages, each a set of jobs that `needs` the one before: `1 static` (every guard and
self-test that needs no server, then `dotnet build -c Release -warnaserror`), `2 test` (one leg per .NET runtime),
`3 package` and `3 aot-smoke` (the packages with the consumer matrix, and the NativeAOT smoke on both runtimes), and
`4 demo-sqlite` and `4 demo-postgres`. A stage starts only when the one before it passed, so a broken gate or build
stops the run early, and the jobs of one stage run in parallel; the test legs build again rather than take the build
of stage 1 (one extra build in the run, and no artifact hand-off). `scripts/StageShape.cs` fails when a job loses its
stage name, its `needs` or a gate step of its stage, when a job-level `if:` or a `continue-on-error` could skip a stage,
or when an uploaded artifact has no `retention-days`; the test results are kept for 7 days.

`.github/workflows/retention.yml` cleans the history of workflow runs by itself, with no switch to turn on and no step
for a person. It runs after every finished `ci` run (on any branch or pull request), once a day as a backstop, and on
request, and on each of them it deletes: it keeps the newest 20 workflow runs of the repository, counted over all
workflows together (the `keep` input of a manual run, never fewer than 5), and deletes every older completed run, which
deletes its logs and artifacts. The runs of the retention workflow itself, one for each finished `ci` run, are left out
of that count so that they cannot push a `ci` run out of the 20, and are trimmed on their own to the newest 5. It never
deletes the run it is executing in nor a run that is queued, in progress or waiting, and a run that is already gone
counts as deleted. The only workflow that uploads artifacts is `ci` (the test results, kept for 7 days), so deleting
the runs leaves no artifact behind. A manual run has a `dry_run` input, false by default, to only list what would be
deleted. GitHub starts a workflow that listens to another workflow only from the default branch, so the automatic runs
start once the file is on `main`. Its token has `actions: write` and `contents: read` only, overlapping runs queue one
after the other, and `scripts/RunRetention.cs` calls the GitHub API for the list of runs and for the deletion of one
run, nothing else, so no branch, tag, release or file is touched. `scripts/RunRetention.cs -- self-test` checks the
selection (fewer runs than the limit, exactly the limit, more, unsorted input, the current run, runs that are not
completed, a limit below 5, ties in the creation time, the one count over all workflows, and the retention runs left
out of it and trimmed to 5) and the dry-run setting, and `scripts/RetentionShape.cs` pins the workflow: its three
triggers, the `false` default of `dry_run`, that nothing gates the deletion, and the count that leaves the retention
runs out; both run in stage 1.

`dotnet run scripts/PackCheck.cs -- package artifacts` also runs `dotnet run scripts/ReadmeCheck.cs -- all artifacts`:
each of the three packages must carry `src/PackageReadme/README.md` as its `README.md`, that file must have the
Status, Install, Quickstart, Providers and Documentation sections, name the three packages, link only to public URLs
of this repository, give no local-feed step (`dotnet pack`, `nuget add source`) and no line over 120 characters, and
every file that git tracks, this script included, must be free of references to documentation that is held outside
the repository (the patterns are assembled at the top of the script, so that the script does not spell them). Every
tracked file is scanned whatever its name, except a binary one (a NUL byte in its first 8000 bytes); an ignored or
untracked file is never scanned, so an agent's local files cannot turn the check red. The same run refuses a
`src/Packaging.props` that does not pack exactly the consumer README (item, `PackagePath`, `Pack`, `PackageReadmeFile`),
a `scripts/PackCheck.cs` that does not call its guard function with `all` and test the exit code, and a
`.github/workflows/ci.yml` whose `package` job does not run `ReadmeCheck.cs -- self-test`, `ReadmeCheck.cs -- source`
and `PackCheck.cs -- package` in that order. `ReadmeCheck.cs -- self-test` feeds each rule a violating and a clean
input, the tree rules each in a throwaway git repository started without the caller's `GIT_*` variables and git
configuration; CI runs it first, then `-- source` as a step of its own, which catches a removed call in
`PackCheck.cs`, then `PackCheck.cs -- package artifacts`.

## Repository layout

- `src/Generator`: the net10.0 analyzer (entity and migration generators and the capability analyzer; they render through Neemle.CSharpComposer)
- `src/Runtime`: the runtime assembly (attributes, migrations, query layer, history, soft delete)
- `src/Sql`: the standard SQL grammar kit the providers share
- `src/Postgres`, `src/Sqlite`: the providers; each packs its assembly, its capability
  manifest and the props file that declares it to the build
- `src/Core`: the core packer: analyzer in `analyzers/dotnet/cs`, runtime and Sql kit in `lib/net10.0`, build
  assets in `buildTransitive`
- `src/Packaging.props`: the metadata the three packages share
- `src/Directory.Build.props`, `tests/Directory.Build.props`: a project is named by its folder, so `src/Postgres` builds
  the assembly, root namespace and package id `Neemle.OrmLite.Postgres`; the prefix is written once, as `OrmLiteRoot` in
  the root `Directory.Build.props`. A file's namespace is the root namespace plus its folder path, and the build fails
  (IDE0130 as an error, set in the `.editorconfig` files under `src/` and `tests/`) when one is not. The exceptions are
  folders with their own `.editorconfig` that gives the reason: the public diagnostics types of
  `src/Runtime/Diagnostics` stay in `Neemle.OrmLite`, and `tests/Tests/Corpus/Support` keeps
  `Neemle.OrmLite.Tests.Corpus` because a golden embeds it
- `src/PackageAssets`: the SVG logo and icon originals, plus the 128 x 128 `icon.png` used by all three packages;
  [the asset guide](../src/PackageAssets/README.md) covers the light and dark variants and how to regenerate the PNG
- `CHANGELOG.md`: what changed in each version of the packages
- `src/PackageReadme/README.md`: the short consumer README that is packed into all three packages, in place of this
  file; it links only to public URLs of this repository
- `tests/Tests`: the library's own tests (the generator driven over fixtures, the query layer over an
  in-process SQLite; the PostgreSQL half needs a server, see "Build, test, pack, demo")
- `tests/compose.yaml`, `tests/skip-allowlist.txt`, `tests/skip-witness.txt`: the throwaway PostgreSQL that is the local
  twin of the CI test job's service, and the two lists the skip gate reads
- `tests/PackConsumer`: the throwaway consumers that `scripts/PackCheck.cs` builds from the packed package
- `scripts/`: thirteen file-based C# tools (`dotnet run scripts/X.cs`), no shell: `PackCheck.cs` (package entries, the
  consumer matrix, the demo guard), `ReadmeCheck.cs` (the packed README is the consumer README, and no file names
  documentation outside this repository), `PackVersions.cs` (every packed assembly carries the package version),
  `NupkgTool.cs` (lists or shows a package's entries), `CorpusCapture.cs` (the rendered-text corpus), `SkipGate.cs`
  (fails the test job on a skipped test that is not allow-listed), `WorkflowShape.cs` (fails when the workflow drops
  the compose step, the variable or the gate), `StepShape.cs` (fails when a test step loses its one-thread cap or
  the zone step and its gate go, or the main test step its 15-minute timeout and its console log),
  `MatrixShape.cs` (fails when the test and AOT jobs lose a .NET 10 or .NET 11 leg or run a leg on the wrong runtime),
  `ComposeCoherence.cs` (the PostgreSQL image major and the two host ports are written in the compose files and
  must agree in CI and the development and demo documentation), `StageShape.cs` (fails when a CI job loses its stage, its `needs` or a
  gate step of its stage), `RetentionShape.cs` (fails when the retention workflow widens its triggers, its permissions
  or its steps, or gets a gate on its deletion) and `RunRetention.cs` (the retention script itself)
- `.github/workflows/ci.yml`: four ordered stages. Stage 1 runs the guards and a strict build; stage 2 builds and tests
  against the PostgreSQL of `tests/compose.yaml`, with the skip gate, once on .NET 10 and once on .NET 11; stage 3 packs
  with the consumer matrix and the guards that need the packages, and runs the NativeAOT smoke consumer on both
  runtimes; stage 4 runs the demo on SQLite (no service) and on PostgreSQL (the server of `demo/compose.yaml`)
- `.github/workflows/retention.yml`: after every finished `ci` run, it keeps the newest 20 workflow runs and
  deletes the older ones with their artifacts, with no manual step (see "Build, test, pack, demo")
- `demo/`: the demo, a consumer of the packages, with an `appsettings.json` block that names the provider and a
  `compose.yaml` for a throwaway PostgreSQL
