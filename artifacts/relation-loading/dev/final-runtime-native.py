import pathlib,subprocess,json
p=pathlib.Path('artifacts/relation-loading/dev').resolve();root=pathlib.Path.cwd();net=(p/'network-v3-id.txt').read_text()
def gate(name,args,version=11):
 argv=['docker','run','--rm','--label','orml.task=ORML-109','--label','orml.actor=dev-policy','--network',net,'-e','ORMLITE_TEST_POSTGRES=Host=pg;Username=postgres;Database=postgres','-e','ORMLITE_EXPECT_DOTNET='+str(version),'-e','DOTNET_ROLL_FORWARD=Major','-e','DOTNET_CLI_USE_MSBUILD_SERVER=0','-e','MSBUILDUSESERVER=0','-v',str(root)+':'+str(root),'-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v','orml-107-nuget:/root/.nuget/packages','-w',str(root),'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+str(root),'orml108-graph-native-sdk'+str(version)+':verified']+args
 r=subprocess.run(['python3',str(p/'run.py'),str(p/name)]+argv,capture_output=True,text=True)
 print(name,r.returncode,flush=True)
 if r.returncode:print(r.stdout[-2000:]);raise RuntimeError(name)
for v in [10,11]:
 for provider in ['sqlite','postgres']:
  gate('final-runtime'+str(v)+'-'+provider,['dotnet','run','--project',str(p/'consumer/PolicySmoke.csproj'),'-c','Release','--artifacts-path',str(p/('final-consumer'+str(v)+'-build')),'--',provider],v)
for v in [10,11]:
 script='dotnet publish '+str(p/'consumer/PolicySmoke.csproj')+' -c Release -r linux-arm64 -p:PolicySmokeNative=true -p:PolicySmokeTfm=net'+str(v)+'.0 --artifacts-path '+str(p/('final-native'+str(v)+'-build'))+' -o '+str(p/('final-native'+str(v)+'-out'))+' -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath='+str(p/('final-native'+str(v)+'-generated'))+' -m:1 -warnaserror && '+str(p/('final-native'+str(v)+'-out/PolicySmoke'))+' sqlite && '+str(p/('final-native'+str(v)+'-out/PolicySmoke'))+' postgres'
 gate('final-native'+str(v),['sh','-c',script],v)
