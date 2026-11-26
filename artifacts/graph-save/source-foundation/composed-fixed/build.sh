set -eu
dotnet build src/Runtime/Runtime.csproj -c Release --artifacts-path "$PROOF/build" -m:1 -p:UseSharedCompilation=false --nologo
dotnet build src/Generator/Generator.csproj -c Release --artifacts-path "$PROOF/build" -m:1 -p:UseSharedCompilation=false --nologo
