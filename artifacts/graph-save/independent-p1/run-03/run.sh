set -eu
dotnet test tests/Tests/Tests.csproj -c Release --artifacts-path "$BUILD" -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~Neemle.OrmLite.Tests.GraphSave.GeneratedSaveConsumerTests --logger 'trx;LogFileName=generated-consumer.trx' --results-directory "$PROOF/results" --nologo
