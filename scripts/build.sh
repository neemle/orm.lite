#!/usr/bin/env bash
# Build the library, its tests and nothing else (the demo is built by scripts/demo.sh against the packed package).
#   scripts/build.sh [dotnet build arguments]
# The first restore of a fresh clone needs nuget.org for the third-party packages the tests use.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet build Neemle.Orm.Lite.slnx -c Release "$@"
