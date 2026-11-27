set -eu
dotnet test tests/Tests/Tests.csproj -c Release --artifacts-path "$PROOF/build" -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Neemle.OrmLite.Tests.GraphSave.SaveIdentifierCompatibilityTests' --logger 'trx;LogFileName=identifier-compatibility.trx' --results-directory "$PROOF/results" --nologo
