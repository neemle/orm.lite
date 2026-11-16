#!/usr/bin/env bash
# Restore, build and pack Neemle.Orm.Lite into ./artifacts (the local feed that nuget.config and the demo restore
# from), then prove the package from a throwaway consumer matrix.
#   scripts/pack.sh                       restore (locked), build, pack, check the package, run the consumer matrix
#   scripts/pack.sh --regenerate-locks    restore without locked mode first (after a PackageReference edit)
#   ORMLITE_VERSION=<v> overrides the one version property for a throwaway pack.
# Nothing is published anywhere: the only output is artifacts/Neemle.Orm.Lite.<version>.nupkg.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
ART="$ROOT/artifacts"
PROJECTS="src/Neemle.Orm.Lite.Generator src/Neemle.Orm.Lite.Runtime src/Neemle.Orm.Lite"
EXTRA=()
[ -z "${ORMLITE_VERSION:-}" ] || EXTRA+=("-p:OrmLiteVersion=${ORMLITE_VERSION}")
REGENERATE=0
for arg in "$@"; do
  case "$arg" in
    --regenerate-locks) REGENERATE=1 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

die() { echo "FAIL: $*" >&2; exit 1; }

for p in $PROJECTS; do
  if [ "$REGENERATE" -eq 1 ]; then
    dotnet restore "$p" --force-evaluate -p:RestoreLockedMode=false "${EXTRA[@]+"${EXTRA[@]}"}"
  else
    dotnet restore "$p" "${EXTRA[@]+"${EXTRA[@]}"}"
  fi
done

mkdir -p "$ART"
find "$ART" -maxdepth 1 -name 'Neemle.Orm.Lite.*.nupkg' -type f -delete

dotnet build src/Neemle.Orm.Lite/Neemle.Orm.Lite.csproj -c Release --no-restore -m:1 "${EXTRA[@]+"${EXTRA[@]}"}"
dotnet pack src/Neemle.Orm.Lite/Neemle.Orm.Lite.csproj -c Release --no-restore "${EXTRA[@]+"${EXTRA[@]}"}"

NUPKG="$(find "$ART" -maxdepth 1 -name 'Neemle.Orm.Lite.*.nupkg' | sort | head -n 1)"
[ -n "$NUPKG" ] || die "pack produced no nupkg"
echo "==> packed $NUPKG"

entries="$(dotnet run scripts/NupkgTool.cs -- entries "$NUPKG" | cut -f2)"
printf '%s\n' "$entries"
for required in analyzers/dotnet/cs/Neemle.Orm.Lite.Generator.dll lib/net10.0/Neemle.Orm.Lite.Runtime.dll \
    buildTransitive/Neemle.Orm.Lite.props buildTransitive/Neemle.Orm.Lite.targets README.md; do
  printf '%s\n' "$entries" | grep -Fxq "$required" || die "the package lacks $required"
done
echo "PASS: the package carries the analyzer, the runtime and the build assets under one version."

bash scripts/pack-consumer.sh "$NUPKG"
