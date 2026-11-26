#!/usr/bin/env sh
set -eu
root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
for project in Core Sqlite Postgres; do
  dotnet pack "$root/src/$project/$project.csproj" -c Release \
    --artifacts-path "$root/artifacts/one-to-one/demo/pack-build" \
    -o "$root/artifacts/one-to-one/demo/packages" -m:1
done
