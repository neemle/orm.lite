set -eu
dotnet --info
dotnet test tests/Tests/Tests.csproj -c Release --artifacts-path "$PROOF/build" -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~Neemle.OrmLite.Tests.GraphSave.GeneratedSaveConsumerTests --logger 'trx;LogFileName=generated-consumer.trx' --results-directory "$PROOF/results" --nologo
