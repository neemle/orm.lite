#!/usr/bin/env sh
set -eu
root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
for project in Core Sqlite Postgres; do
  dotnet pack "$root/src/$project/$project.csproj" -c Release \
    --artifacts-path "$root/artifacts/foreign-keys/demo-parity/pack-build" \
    -o "$root/artifacts/foreign-keys/demo-parity/packages" -m:1
done
