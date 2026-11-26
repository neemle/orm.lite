import json,pathlib,subprocess,sys
P=pathlib.Path('artifacts/graph/fk-composition-final').resolve();base=json.loads((P/'docker-base.json').read_text())
for name in ['Core','Sqlite','Postgres']:
 a=base+['sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27','dotnet','pack','src/'+name+'/'+name+'.csproj','-c','Release','--artifacts-path',str(P/'pack-build'),'-o',str(P/'feed'),'-p:RestoreConfigFile='+str(P/'nuget.config'),'-p:UseSharedCompilation=false','-m:1']
 code=subprocess.call(['python3',str(P/'run.py'),'pack-'+name,*a])
 if code:sys.exit(code)
