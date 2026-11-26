TWELVE-EIGHT-GATE-EVIDENCE v1 pack-frozen107 ORML-107

**Gate** pack-frozen107

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 orml108-graph-native-sdk10:verified sh examples/calculated/prepare-packages.sh
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:54:55.490094+00:00 -> 2026-11-26T09:55:14.518509+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log pack-frozen107.log.

---

TWELVE-EIGHT-GATE-EVIDENCE v1 jit10-sqlite ORML-107

**Gate** jit10-sqlite

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo --network 869a499133714e2417f405b8afe11b4e519cb98ca437038295e61520673c23b8 -e 'CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo' -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated orml108-graph-native-sdk10:verified dotnet run --project examples/calculated/CalculatedDemo.csproj -c Release -p:DemoTfm=net10.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/jit10-build -p:UseSharedCompilation=false -- sqlite
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:56:12.768609+00:00 -> 2026-11-26T09:56:51.930578+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log jit10-sqlite.log.

---

TWELVE-EIGHT-GATE-EVIDENCE v1 jit10-postgres ORML-107

**Gate** jit10-postgres

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo --network 869a499133714e2417f405b8afe11b4e519cb98ca437038295e61520673c23b8 -e 'CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo' -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated orml108-graph-native-sdk10:verified dotnet run --project examples/calculated/CalculatedDemo.csproj -c Release -p:DemoTfm=net10.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/jit10-build -p:UseSharedCompilation=false -- postgres
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:56:52.016988+00:00 -> 2026-11-26T09:56:58.249641+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log jit10-postgres.log.

---

TWELVE-EIGHT-GATE-EVIDENCE v1 jit11-sqlite ORML-107

**Gate** jit11-sqlite

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo --network 869a499133714e2417f405b8afe11b4e519cb98ca437038295e61520673c23b8 -e 'CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo' -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated orml108-graph-native-sdk11:verified dotnet run --project examples/calculated/CalculatedDemo.csproj -c Release -p:DemoTfm=net11.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/jit11-build -p:UseSharedCompilation=false -- sqlite
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:56:58.359930+00:00 -> 2026-11-26T09:57:10.392251+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log jit11-sqlite.log.

---

TWELVE-EIGHT-GATE-EVIDENCE v1 jit11-postgres ORML-107

**Gate** jit11-postgres

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo --network 869a499133714e2417f405b8afe11b4e519cb98ca437038295e61520673c23b8 -e 'CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo' -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated orml108-graph-native-sdk11:verified dotnet run --project examples/calculated/CalculatedDemo.csproj -c Release -p:DemoTfm=net11.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/jit11-build -p:UseSharedCompilation=false -- postgres
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:57:10.531608+00:00 -> 2026-11-26T09:57:14.702445+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log jit11-postgres.log.

---

TWELVE-EIGHT-GATE-EVIDENCE v1 native10 ORML-107

**Gate** native10

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo --network 869a499133714e2417f405b8afe11b4e519cb98ca437038295e61520673c23b8 -e 'CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo' -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated orml108-graph-native-sdk10:verified sh -c 'dotnet publish examples/calculated/CalculatedDemo.csproj -c Release -r linux-arm64 -p:DemoNative=true -p:DemoTfm=net10.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native10-build -o /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native10-out -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native10-generated -p:UseSharedCompilation=false -m:1 && /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native10-out/CalculatedDemo sqlite && /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native10-out/CalculatedDemo postgres'
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:57:14.784847+00:00 -> 2026-11-26T09:59:02.022624+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log native10.log.

---

TWELVE-EIGHT-GATE-EVIDENCE v1 native11 ORML-107

**Gate** native11

**Command (full argv)**
```sh
docker run --rm --label orml.task=ORML-107 --label orml.actor=calculated-demo --network 869a499133714e2417f405b8afe11b4e519cb98ca437038295e61520673c23b8 -e 'CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo' -e DOTNET_ROLL_FORWARD=Major -e DOTNET_CLI_USE_MSBUILD_SERVER=0 -e MSBUILDUSESERVER=0 -v /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated:/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -v /Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro -v orml-107-nuget:/root/.nuget/packages -w /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated orml108-graph-native-sdk11:verified sh -c 'dotnet publish examples/calculated/CalculatedDemo.csproj -c Release -r linux-arm64 -p:DemoNative=true -p:DemoTfm=net11.0 --artifacts-path /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native11-build -o /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native11-out -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native11-generated -p:UseSharedCompilation=false -m:1 && /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native11-out/CalculatedDemo sqlite && /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated/artifacts/calculated/demo/native11-out/CalculatedDemo postgres'
```

**Working Directory** /Users/para/work/tskr/libs/orm.lite/artifacts/worktrees/orml-107-calculated

**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.

**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.

**Exit Code** 0

**Commit** c9fd3ed772eadc68119dab56b66b460fa88d73f2; source tree 6659eec50c5429160ba38a53679a91f0b9391f4b

**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.

**Run Position** 2026-11-26T09:59:02.128675+00:00 -> 2026-11-26T10:01:15.110224+00:00; functional inputChanged=False

**Disposition** PASS for this command only. Full log native11.log.
