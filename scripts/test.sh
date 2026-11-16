#!/usr/bin/env bash
# Run the library's own tests. They drive the generator over fixture sources and the query layer over an
# in-process SQLite, so no database and no container are needed.
#   scripts/test.sh                      the whole suite
#   scripts/test.sh --filter <expr>      any dotnet test arguments are passed through
# A test host that dies mid-run still prints a pass line, so a full run also checks that it reported at least as
# many tests as the build discovers.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
PROJECT=tests/Neemle.Orm.Lite.Tests/Neemle.Orm.Lite.Tests.csproj
log="$(mktemp)"
trap 'rm -f "$log" "$log.list"' EXIT

dotnet restore "$PROJECT"
dotnet build "$PROJECT" -c Release --no-restore -m:1
rc=0
dotnet test "$PROJECT" -c Release --no-build "$@" 2>&1 | tee "$log" || rc=1

if grep -q 'No test matches the given testcase filter' "$log"; then
  echo "FAIL: the filter matched no test, so nothing ran."
  rc=1
fi

if [ "$#" -eq 0 ]; then
  executed="$(sed -n 's/.*Total: *\([0-9][0-9]*\).*/\1/p' "$log" | awk '{s+=$1} END {print s+0}')"
  dotnet test "$PROJECT" -c Release --no-build --list-tests 2>/dev/null > "$log.list" || true
  floor="$(awk '/^The following Tests are available:/ {on=1; next} on && /^    / {n++} END {print n+0}' "$log.list")"
  echo "--- discovered: $floor, executed: $executed"
  if [ "$floor" -eq 0 ] || [ "$executed" -lt "$floor" ]; then
    echo "FAIL: the run reported fewer tests ($executed) than the build discovers ($floor)."
    rc=1
  fi
  grep -q 'Passed!' "$log" && ! grep -qE 'Failed!|Test Run Aborted|aborted' "$log" || rc=1
fi

exit "$rc"
