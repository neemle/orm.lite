#!/bin/sh
set -eu
TASK_OUTPUT='/Users/para/work/tskr/libs/orm.lite/.git/agent-worktrees/orml-108-graphs/artifacts/graph/dev-migration-layout'
dotnet --info
dotnet pack Neemle.OrmLite.slnx -c Release --artifacts-path "$TASK_OUTPUT/build" -o "$TASK_OUTPUT/feed" -p:RestorePackagesPath="$TASK_OUTPUT/packages" -p:RestoreConfigFile="$TASK_OUTPUT/nuget.config" -m:1
dotnet build demo/OrmLiteDemo.csproj -c Release --artifacts-path "$TASK_OUTPUT/consumer" -p:RestorePackagesPath="$TASK_OUTPUT/packages" -p:RestoreConfigFile="$TASK_OUTPUT/nuget.config" -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath="$TASK_OUTPUT/generated" -warnaserror -m:1
