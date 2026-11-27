set -eu
dotnet test tests/Tests/Tests.csproj -c Release --no-restore --artifacts-path "$BUILD" -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Neemle.OrmLite.Tests.GraphSave.SaveMaterializerTests|FullyQualifiedName~Neemle.OrmLite.Tests.GraphSave.SaveLateProjectionTests' --logger 'trx;LogFileName=materializers-bound-seed.trx' --results-directory "$PROOF/results" --nologo
