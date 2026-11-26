from pathlib import Path
import subprocess,json
p=Path('artifacts/calculated/demo').resolve();root=Path.cwd();net=(p/'network-id.txt').read_text().strip()
def gate(name,command,v):
 argv=['docker','run','--rm','--label','orml.task=ORML-107','--label','orml.actor=calculated-demo','--network',net,'-e','CALCULATED_POSTGRES=Host=pg;Username=postgres;Database=calculated_demo','-e','DOTNET_ROLL_FORWARD=Major','-e','DOTNET_CLI_USE_MSBUILD_SERVER=0','-e','MSBUILDUSESERVER=0','-v',str(root)+':'+str(root),'-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v','orml-107-nuget:/root/.nuget/packages','-w',str(root),'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+str(root),'orml108-graph-native-sdk'+str(v)+':verified']+command
 result=subprocess.run(['python3',str(p/'run.py'),str(p/name)]+argv)
 if result.returncode:raise SystemExit(result.returncode)
for v in [10,11]:
 for provider in ['sqlite','postgres']:
  gate('jit'+str(v)+'-'+provider,['dotnet','run','--project','examples/calculated/CalculatedDemo.csproj','-c','Release','-p:DemoTfm=net'+str(v)+'.0','--artifacts-path',str(p/('jit'+str(v)+'-build')),'-p:UseSharedCompilation=false','--',provider],v)
for v in [10,11]:
 command='dotnet publish examples/calculated/CalculatedDemo.csproj -c Release -r linux-arm64 -p:DemoNative=true -p:DemoTfm=net'+str(v)+'.0 --artifacts-path '+str(p/('native'+str(v)+'-build'))+' -o '+str(p/('native'+str(v)+'-out'))+' -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath='+str(p/('native'+str(v)+'-generated'))+' -p:UseSharedCompilation=false -m:1 && '+str(p/('native'+str(v)+'-out/CalculatedDemo'))+' sqlite && '+str(p/('native'+str(v)+'-out/CalculatedDemo'))+' postgres'
 gate('native'+str(v),['sh','-c',command],v)
