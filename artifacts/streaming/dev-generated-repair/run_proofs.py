import json,pathlib,subprocess
b=pathlib.Path('artifacts/streaming/dev-generated-repair')
root=str(pathlib.Path.cwd());common=['docker','run','--rm','--label','orml.task=ORML-116','--label','orml.actor=dev-generated-repair','-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v',root+':'+root,'-v','orml-107-nuget:/root/.nuget/packages','-w',root,'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+root]
def run(name,command,extra=[]):
 argv=common+extra+['orml108-graph-native-sdk10:verified']+command
 code=subprocess.call(['python3','artifacts/streaming/run_gate.py',name.upper().replace('-','_'),str(b/name),'--',*argv])
 if code:raise SystemExit(code)
for mode,name in [('--case-hints','verified-case-hint-probe'),('--provider-version','verified-provider-version')]:
 run(name,['dotnet','run','--project',str(b/'capture/capture.csproj'),'--artifacts-path',str(b/'verified-capture-build'),'--no-build','--',mode],['--network','orml116-gen-repair','-e','ORMLITE_TEST_POSTGRES=Host=gen-pg;Username=postgres;Database=postgres'])
for name,file in [('verified-source-metrics','verified-source-files.txt'),('verified-generated-metrics','verified-generated-files.txt')]:
 run(name,['dotnet','run','--project','artifacts/streaming/metrics/Metrics.csproj','--artifacts-path',str(b/'metrics-build'),'--no-build','--',str(b/file)])
run('verified-token-proof',['dotnet','run','--project',str(b/'proof/proof.csproj'),'--artifacts-path',str(b/'proof-build')])
