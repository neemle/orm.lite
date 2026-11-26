from pathlib import Path
import subprocess
b=Path('artifacts/foreign-keys/dev-migration-layout');root=str(Path.cwd());common=['docker','run','--rm','--label','orml.task=ORML-110','--label','orml.actor=dev-migration-layout','-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v',root+':'+root,'-v','orml-107-nuget:/root/.nuget/packages','-w',root,'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+root]
def run(name,args,extra=[]):
 if subprocess.call(['python3','artifacts/foreign-keys/run_gate.py','FK_MIGRATION_'+name.upper().replace('-','_'),str(b/name),'--',*common,*extra,'orml108-graph-native-sdk10:verified',*args]):raise SystemExit(1)
run('generated-metrics',['dotnet','run','--project','artifacts/foreign-keys/metrics/Metrics.csproj','--artifacts-path',str(b/'metrics-build'),'--no-build','--',str(b/'generated-files.txt')])
run('token-proof',['dotnet','run','--project',str(b/'proof/proof.csproj'),'--artifacts-path',str(b/'proof-build')])
run('provider-version',['dotnet','run','--project',str(b/'capture/capture.csproj'),'--artifacts-path',str(b/'after-build'),'--no-build','--','--versions'],['--network','orml110-migration-layout','-e','ORMLITE_TEST_POSTGRES=Host=mig-pg;Username=postgres;Database=postgres'])
