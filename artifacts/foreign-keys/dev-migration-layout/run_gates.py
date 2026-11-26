import pathlib,json,subprocess
b=pathlib.Path('artifacts/foreign-keys/dev-migration-layout');root=str(pathlib.Path.cwd())
assert json.loads((b/'unit-final.json').read_text())['exitCode']==0
for name in ['providers10','providers11']:
 old=json.loads(pathlib.Path('artifacts/foreign-keys/sdet-final/'+name+'.json').read_text());a=old['argv'];network=a.index('--network');a[network+1]='orml110-migration-layout'
 a=[x.replace('artifacts/foreign-keys/sdet-final','artifacts/foreign-keys/dev-migration-layout').replace('orml110-sdet-final','orml110-migration').replace('orml.owner=sdet-final','orml.actor=dev-migration-layout').replace('Host=fk-pg;','Host=mig-pg;') for x in a]
 # Replace the exact admitted server hostname from the previous fixture; preserve every filter/assertion.
 a=[('ORMLITE_TEST_POSTGRES=Host=mig-pg;Username=postgres;Database=postgres' if x.startswith('ORMLITE_TEST_POSTGRES=') else x) for x in a]
 code=subprocess.call(['python3','artifacts/foreign-keys/run_gate.py','FK_MIGRATION_'+name.upper(),str(b/name),'--',*a])
 if code:raise SystemExit(code)
common=['docker','run','--rm','--label','orml.task=ORML-110','--label','orml.actor=dev-migration-layout','-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v',root+':'+root,'-v','orml-107-nuget:/root/.nuget/packages','-w',root,'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+root]
for major in ['10','11']:
 cmd=f'dotnet publish tests/ForeignKeysAot/ForeignKeysAot.csproj -c Release -r linux-arm64 -p:AotTfm=net{major}.0 --artifacts-path {b}/native{major}-build -o {b}/native{major}-out -m:1 -warnaserror && {b}/native{major}-out/ForeignKeysAot'
 a=common+[f'orml108-graph-native-sdk{major}:verified','sh','-c',cmd]
 if subprocess.call(['python3','artifacts/foreign-keys/run_gate.py','FK_MIGRATION_NATIVE_'+major,str(b/('native'+major)),'--',*a]):raise SystemExit(1)
