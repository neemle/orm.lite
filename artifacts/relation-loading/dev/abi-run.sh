#!/bin/sh
set -eu
TASK_OUT="$PWD/artifacts/relation-loading/dev"
dotnet build "$TASK_OUT/abi/abi.csproj" -c Release --artifacts-path "$TASK_OUT/abi-old-build" -m:1 -warnaserror
dotnet "$TASK_OUT/abi-old-build/bin/abi/release/AbiProbe.dll"
dotnet build "$TASK_OUT/abi/abi.csproj" -c Release --artifacts-path "$TASK_OUT/abi-current-build" -p:PolicyAbiRuntime="$TASK_OUT/consumer-build/bin/Runtime/release/Neemle.OrmLite.Runtime.dll" -m:1 -warnaserror
dotnet "$TASK_OUT/abi-current-build/bin/abi/release/AbiProbe.dll" current
mkdir -p "$TASK_OUT/abi-overlay"
cp "$TASK_OUT/abi-old-build/bin/abi/release/"* "$TASK_OUT/abi-overlay/"
cp "$TASK_OUT/consumer-build/bin/Runtime/release/Neemle.OrmLite.Runtime.dll" "$TASK_OUT/abi-overlay/"
dotnet "$TASK_OUT/abi-overlay/AbiProbe.dll" current
