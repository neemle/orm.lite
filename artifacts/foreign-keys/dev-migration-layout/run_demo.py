from pathlib import Path
import json,subprocess
b=Path('artifacts/foreign-keys/dev-migration-layout');assert json.loads((b/'pack-build.json').read_text())['exitCode']==0
for provider,name in [('sqlite','sqlite-final'),('postgres','postgres-final')]:
 a=json.loads(Path('artifacts/foreign-keys/demo/'+name+'.json').read_text())['argv']
 a=[x.replace('artifacts/foreign-keys/demo','artifacts/foreign-keys/dev-migration-layout').replace('orml.actor=dev-demo','orml.actor=dev-migration-layout') for x in a]
 if '--network' in a:a[a.index('--network')+1]='orml110-migration-layout'
 code=subprocess.call(['python3','artifacts/foreign-keys/run_gate.py','FK_MIGRATION_FULL_PACKED_DEMO_'+provider.upper(),str(b/('demo-'+provider)),'--',*a])
 if code:raise SystemExit(code)
