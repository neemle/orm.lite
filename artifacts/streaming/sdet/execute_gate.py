import argparse,pathlib,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('gate');p.add_argument('sdk');p.add_argument('mode',choices=['scoped','publish','native','coverage']);a=p.parse_args()
w=str(pathlib.Path.cwd());prefix='artifacts/streaming/sdet/'+a.gate
argv=['docker','run','--rm','--name','orml116-sdet-'+a.gate,'--label','orml.task=ORML-116','--label','orml.actor=sdet','--network','orml116-sdet-6b34695','-v',str(pathlib.Path(w).parents[1])+':'+str(pathlib.Path(w).parents[1])+':ro','-v',w+':'+w,'-v','orml-107-nuget:/root/.nuget/packages','-w',w,'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+w,'-e','ORMLITE_TEST_POSTGRES=Host=sdet-pg;Username=postgres;Database=postgres','-e','DOTNET_ROLL_FORWARD='+('Minor' if a.sdk=='10' else 'LatestMajor'),'-e','ORMLITE_EXPECT_DOTNET='+a.sdk+'.0']
if a.mode=='coverage':argv+=['--platform','linux/amd64']
argv+=['mcr.microsoft.com/dotnet/sdk:10.0' if a.mode=='coverage' else 'orml108-graph-native-sdk'+a.sdk+':verified']
if a.mode=='scoped':argv+=['dotnet','test','tests/Tests/Tests.csproj','-c','Release','--artifacts-path',prefix+'-build','--filter','FullyQualifiedName~Streaming|FullyQualifiedName~TestHostRuntimeTests','--logger','trx','--results-directory',prefix+'-results']
if a.mode=='publish':argv+=['dotnet','publish','tests/StreamingAot/StreamingAot.csproj','-c','Release','-r','linux-arm64','-p:StreamingTfm=net'+a.sdk+'.0','--artifacts-path',prefix+'-build','-o','artifacts/streaming/sdet/native'+a.sdk+'-out']
if a.mode=='native':argv+=['artifacts/streaming/sdet/native'+a.sdk+'-out/Neemle.OrmLite.StreamingAot']
if a.mode=='coverage':argv+=['dotnet','test','tests/Tests/Tests.csproj','-c','Release','--artifacts-path',prefix+'-build','--filter','FullyQualifiedName~Streaming|FullyQualifiedName~TestHostRuntimeTests','--logger','trx','--results-directory',prefix+'-results','--collect:Code Coverage;Format=xml']
sys.exit(subprocess.call([sys.executable,'artifacts/streaming/sdet/run_gate.py',a.gate,prefix,'--']+argv))
