set -eu
dotnet test tests/Tests/Tests.csproj -c Release --no-restore --artifacts-path "$BUILD" -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Neemle.OrmLite.Tests.GraphSave.SaveIdentifierCompatibilityTests' --logger 'trx;LogFileName=identifier-compatibility.trx' --results-directory "$PROOF/results" --nologo
