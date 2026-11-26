TWELVE-EIGHT-GATE-EVIDENCE v1 final-runtime10-sqlite ORML-109

**Gate** final-runtime10-sqlite

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=10 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk10:verified dotnet run --project /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/consumer/PolicySmoke.csproj -c Release --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-consumer10-build -- sqlite
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:24:15.994759+00:00 -> 2026-11-26T09:24:38.270580+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: final-runtime10-sqlite.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 final-runtime10-postgres ORML-109

**Gate** final-runtime10-postgres

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=10 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk10:verified dotnet run --project /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/consumer/PolicySmoke.csproj -c Release --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-consumer10-build -- postgres
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:24:38.855220+00:00 -> 2026-11-26T09:24:46.426111+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: final-runtime10-postgres.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 final-runtime11-sqlite ORML-109

**Gate** final-runtime11-sqlite

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=11 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk11:verified dotnet run --project /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/consumer/PolicySmoke.csproj -c Release --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-consumer11-build -- sqlite
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:24:46.683175+00:00 -> 2026-11-26T09:25:11.753012+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: final-runtime11-sqlite.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 final-runtime11-postgres ORML-109

**Gate** final-runtime11-postgres

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=11 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk11:verified dotnet run --project /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/consumer/PolicySmoke.csproj -c Release --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-consumer11-build -- postgres
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:25:12.301241+00:00 -> 2026-11-26T09:25:26.343844+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: final-runtime11-postgres.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 final-native10 ORML-109

**Gate** final-native10

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=10 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk10:verified sh -c 'dotnet publish /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/consumer/PolicySmoke.csproj -c Release -r linux-arm64 -p:PolicySmokeNative=true -p:PolicySmokeTfm=net10.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native10-build -o /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native10-out -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native10-generated -m:1 -warnaserror && /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native10-out/PolicySmoke sqlite && /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native10-out/PolicySmoke postgres'
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:25:26.690515+00:00 -> 2026-11-26T09:27:59.246367+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: final-native10.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 final-native11 ORML-109

**Gate** final-native11

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=11 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk11:verified sh -c 'dotnet publish /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/consumer/PolicySmoke.csproj -c Release -r linux-arm64 -p:PolicySmokeNative=true -p:PolicySmokeTfm=net11.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native11-build -o /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native11-out -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native11-generated -m:1 -warnaserror && /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native11-out/PolicySmoke sqlite && /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/final-native11-out/PolicySmoke postgres'
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:28:00.143949+00:00 -> 2026-11-26T09:32:03.866155+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: final-native11.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 public-demo-sqlite ORML-109

**Gate** public-demo-sqlite

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=10 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk10:verified dotnet run --project examples/relation-loading/RelationLoadingDemo.csproj -c Release --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/public-demo-build -- sqlite
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:33:00.458928+00:00 -> 2026-11-26T09:34:04.990995+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: public-demo-sqlite.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 public-demo-postgres ORML-109

**Gate** public-demo-postgres

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=10 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk10:verified dotnet run --project examples/relation-loading/RelationLoadingDemo.csproj -c Release --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/public-demo-build -- postgres
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:34:05.629961+00:00 -> 2026-11-26T09:34:23.075037+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: public-demo-postgres.log

---

TWELVE-EIGHT-GATE-EVIDENCE v1 public-relocation-capture-forced ORML-109

**Gate** public-relocation-capture-forced

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-109 --label orml.actor=dev-policy --network 24e9e515964db514f2cc073dc24f70b5f19290bf9e63b312723a77f7f2c1680d -e 'ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres' -e ORMLITE_EXPECT_DOTNET=10 -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading:/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading orml108-graph-native-sdk10:verified dotnet build examples/relation-loading/RelationLoadingDemo.csproj -c Release --no-restore --artifacts-path /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/public-demo-build -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading/artifacts/relation-loading/dev/public-generated -p:UseSharedCompilation=false -m:1 -t:Rebuild -p:BuildProjectReferences=false
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-109-relation-loading

**Environment** Exact Docker argv above pins SDK image, cache, PostgreSQL fixture and runtime-major check. Image/provider creation evidence: provider-setup-v3.json.

**Scope** Same24 consumer C# sources; public relocation uses their exact copies. Native publishes include both actual binary runs.

**Exit Code** 0

**Commit** 712530c979fe316bfa5e40b1b6d6f933c70432ae; source tree 2b3f01e4be430d703284bb87c5f0792a7eaef978

**Generated** Actual ORM outputs retained; public8/Native8 byte equality: public-relocation-proof.json.

**Run Position** 2026-11-26T09:35:51.671095+00:00 -> 2026-11-26T09:35:55.995226+00:00; sourceChangedDuringRun=False

**Disposition** PASS for this command only; no whole QA, raw coverage, release or merge claim. Full console: public-relocation-capture-forced.log
