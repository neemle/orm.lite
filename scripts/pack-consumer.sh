#!/usr/bin/env bash
# The transitivity matrix. A throwaway consumer restores ONLY from the freshly packed feed, with its own package
# cache, and is built several ways. usage: pack-consumer.sh <path to the packed nupkg>
set -euo pipefail

NUPKG="$(cd "$(dirname "${1:?usage: pack-consumer.sh <nupkg>}")" && pwd)/$(basename "$1")"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(basename "$NUPKG" .nupkg)"; VERSION="${VERSION#Neemle.Orm.Lite.}"
WORK="$(mktemp -d)"
trap 'find "$WORK" -type f -delete; find "$WORK" -depth -type d -empty -delete' EXIT
FEED="$WORK/feed"
mkdir -p "$FEED"
cp "$NUPKG" "$FEED/"
export NUGET_PACKAGES="$WORK/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

failed=0
fail() { echo "FAIL: $*" >&2; failed=1; }

new_consumer() {
  local name="$1" assets="$2" output="${3:-Library}" dir="$WORK/$1"
  mkdir -p "$dir"
  sed -e "s|__VERSION__|$VERSION|" -e "s|__ASSETS__|$assets|" -e "s|__OUTPUT__|$output|" "$HERE/tests/PackConsumer/Consumer.csproj.template" > "$dir/Consumer.csproj"
  cp "$HERE/tests/PackConsumer/Probe.cs" "$dir/Probe.cs"
  cat > "$dir/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="fresh" value="$FEED" />
  </packageSources>
</configuration>
EOF
  echo "$dir"
}

build() {
  local dir="$1" log="$2"
  shift 2
  ( cd "$dir" && dotnet build -c Release -v q --nologo -p:UseSharedCompilation=false -nodeReuse:false "$@" ) > "$log" 2>&1
}

generated_count() { find "$1" -name 'OrmLite.*.g.cs' 2>/dev/null | wc -l | tr -d ' '; }

echo "==> consumer A: default reference gets the generators and the runtime"
A="$(new_consumer a "")"
build "$A" "$WORK/a.log" || { fail "A: the default consumer did not build"; tail -n 30 "$WORK/a.log" >&2; }
for hint in OrmLite.Entities.g.cs OrmLite.Migrations.g.cs; do
  find "$A/gen" -name "$hint" | grep -q . || fail "A: the default reference did not emit $hint (analyzer unreachable)"
done

echo "==> consumer B: ExcludeAssets=analyzers gets the runtime and the actor setting initializer and emits nothing"
B="$(new_consumer b 'ExcludeAssets="analyzers"')"
build "$B" "$WORK/b.log" -p:OrmLiteActorSetting=probe.actor || { fail "B: the runtime-only consumer did not build"; tail -n 30 "$WORK/b.log" >&2; }
if [ "$(generated_count "$B/gen")" != "0" ]; then fail "B: ExcludeAssets=analyzers still emitted generated files"; find "$B/gen" -type f >&2; fi
find "$B/obj" -name 'OrmLiteActorSetting.g.cs' | grep -q . || fail "B: the actor setting initializer did not reach the runtime-only consumer"

echo "==> consumer C: IncludeAssets=analyzers has no runtime and cannot compile"
C="$(new_consumer c 'IncludeAssets="analyzers"')"
if build "$C" "$WORK/c.log"; then
  fail "C: an analyzer-only reference compiled although the runtime types are missing"
elif ! grep -qE "error CS0(234|246|103)" "$WORK/c.log"; then
  fail "C: the analyzer-only consumer failed, but not on a missing runtime type"; tail -n 20 "$WORK/c.log" >&2
fi

echo "==> consumer D: OrmLiteDefaultTenant reaches the generator from the package"
D="$(new_consumer d "")"
cp "$HERE/tests/PackConsumer/TenantProbe.cs" "$D/TenantProbe.cs"
build "$D" "$WORK/d.log" -p:OrmLiteDefaultTenant=OwnerId || { fail "D: the entity with the pinned tenant property did not build"; tail -n 30 "$WORK/d.log" >&2; }
grep -q '"owner_id"' "$D/gen"/*/*/OrmLite.Entities.g.cs 2>/dev/null || fail "D: the pinned tenant column owner_id is absent from the emitted entities"
D2="$(new_consumer d2 "")"
cp "$HERE/tests/PackConsumer/TenantProbe.cs" "$D2/TenantProbe.cs"
if build "$D2" "$WORK/d2.log"; then
  fail "D: the same entity compiled without the property although the default tenant is not OwnerId"
elif ! grep -q "ORMLITE007" "$WORK/d2.log"; then
  fail "D: the unset consumer failed, but not on ORMLITE007"; tail -n 20 "$WORK/d2.log" >&2
fi

echo "==> consumer E: the actor setting initializer travels in buildTransitive and fires without a call"
E1="$(new_consumer e1 "" Exe)"
cp "$HERE/tests/PackConsumer/Program.cs" "$E1/Program.cs"
build "$E1" "$WORK/e1.log" -p:OrmLiteActorSetting=probe.actor || { fail "E: the pinned actor consumer did not build"; tail -n 30 "$WORK/e1.log" >&2; }
got1="$(cd "$E1" && dotnet bin/Release/net10.0/Consumer.dll 2>&1 | tr -d '\r\n')"
[ "$got1" = "probe.actor" ] || fail "E: the pinned consumer printed '$got1', expected probe.actor"
E2="$(new_consumer e2 "" Exe)"
cp "$HERE/tests/PackConsumer/Program.cs" "$E2/Program.cs"
build "$E2" "$WORK/e2.log" || { fail "E: the unpinned actor consumer did not build"; tail -n 30 "$WORK/e2.log" >&2; }
got2="$(cd "$E2" && dotnet bin/Release/net10.0/Consumer.dll 2>&1 | tr -d '\r\n')"
[ "$got2" = "ormlite.actor" ] || fail "E: the unpinned consumer printed '$got2', expected the library default ormlite.actor"

echo "==> consumer F: the nuspec declares no dependency on another library"
if dotnet run "$HERE/scripts/NupkgTool.cs" -- nuspec "$NUPKG" | grep -qi "<dependency "; then
  fail "F: the nuspec declares a package dependency"
  dotnet run "$HERE/scripts/NupkgTool.cs" -- nuspec "$NUPKG" >&2
fi

echo "==> consumer G: a project that only references consumer A gets the runtime and the initializer but no generator"
G="$WORK/g"; mkdir -p "$G"
cat > "$G/Downstream.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(MSBuildProjectDirectory)/gen</CompilerGeneratedFilesOutputPath>
    <RestoreLockedMode>false</RestoreLockedMode>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="gen/**" />
    <ProjectReference Include="../a/Consumer.csproj" />
  </ItemGroup>
</Project>
EOF
cp "$A/nuget.config" "$G/nuget.config"
echo "public static class Downstream { public static System.Type Runtime => typeof(Neemle.Orm.Lite.OrmLiteSettings); }" > "$G/Downstream.cs"
build "$G" "$WORK/g.log" -p:OrmLiteActorSetting=probe.actor || { fail "G: the downstream consumer did not build"; tail -n 30 "$WORK/g.log" >&2; }
[ "$(generated_count "$G/gen")" = "0" ] || fail "G: a project that only references a consumer of the package loaded the generators"
find "$G/obj" -name 'OrmLiteActorSetting.g.cs' | grep -q . || fail "G: the actor setting initializer did not reach a project that never named the package"

if [ "$failed" -ne 0 ]; then echo "FAIL: PackConsumer matrix" >&2; exit 1; fi
echo "PASS: PackConsumer matrix (default, runtime-only, analyzer-only, default tenant, actor setting, nuspec, downstream)."
