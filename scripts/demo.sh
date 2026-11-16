#!/usr/bin/env bash
# Run the demo against a throwaway PostgreSQL.
#   scripts/demo.sh                              packs the library into ./artifacts when the package is not there yet,
#                                                starts a PostgreSQL in Docker, runs the demo, removes the container
#   ORMLITE_DEMO_CONNECTION="Host=...;..." scripts/demo.sh
#                                                uses your own database instead; nothing is started or removed
# The demo restores Neemle.Orm.Lite as a package from ./artifacts, exactly as a consumer would.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VERSION="$(sed -n 's|.*<OrmLiteVersion>\(.*\)</OrmLiteVersion>.*|\1|p' Directory.Build.props)"
NUPKG="artifacts/Neemle.Orm.Lite.${VERSION}.nupkg"
[ -f "$NUPKG" ] || bash scripts/pack.sh

if grep -q 'ProjectReference' demo/OrmLiteDemo.csproj; then
  echo "FAIL: the demo must reference the package, not a project" >&2
  exit 1
fi

export NUGET_PACKAGES="$ROOT/artifacts/.demo-packages"
mkdir -p "$NUGET_PACKAGES"
find "$NUGET_PACKAGES" -mindepth 1 -maxdepth 1 -iname 'neemle.orm.lite' -exec find {} -delete \;

CONTAINER=""
cleanup() {
  [ -z "$CONTAINER" ] || docker rm -f "$CONTAINER" > /dev/null 2>&1 || true
}
trap cleanup EXIT

if [ -z "${ORMLITE_DEMO_CONNECTION:-}" ]; then
  command -v docker > /dev/null || { echo "FAIL: set ORMLITE_DEMO_CONNECTION or install Docker for a throwaway PostgreSQL" >&2; exit 1; }
  CONTAINER="orm-lite-demo-pg-$$"
  docker run -d --name "$CONTAINER" -e POSTGRES_HOST_AUTH_METHOD=trust -p 127.0.0.1::5432 postgres:18-alpine > /dev/null
  for _ in $(seq 1 60); do
    docker exec "$CONTAINER" pg_isready -U postgres > /dev/null 2>&1 && break
    sleep 1
  done
  docker exec "$CONTAINER" pg_isready -U postgres > /dev/null 2>&1 || { echo "FAIL: PostgreSQL did not become ready" >&2; exit 1; }
  PORT="$(docker port "$CONTAINER" 5432/tcp | head -n 1 | sed 's/.*://')"
  export ORMLITE_DEMO_CONNECTION="Host=127.0.0.1;Port=${PORT};Username=postgres;Database=postgres"
fi

OUT="$(mktemp)"
trap 'rm -f "$OUT"; cleanup' EXIT
dotnet run --project demo/OrmLiteDemo.csproj -c Release | tee "$OUT"
grep -qx 'demo ok' "$OUT" || { echo "FAIL: the demo did not finish with 'demo ok'" >&2; exit 1; }
