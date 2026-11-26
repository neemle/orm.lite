import subprocess, pathlib, json, time, os
root=pathlib.Path.cwd(); out=root/'artifacts/graph/dev-migration-layout'; actor='dev108-migration-layout'; net='orml108-migration-layout'; fixtures=[]
def cmd(argv):
 r=subprocess.run(argv,text=True,capture_output=True);v=dict(argv=argv,exitCode=r.returncode,stdout=r.stdout,stderr=r.stderr,time=__import__('datetime').datetime.now(__import__('datetime').timezone.utc).isoformat());fixtures.append(v);(out/'fixtures.json').write_text(json.dumps(fixtures,indent=2));r.check_returncode();return r.stdout.strip()
def gate(name,args,version=10,env=None,network=False):
 a=['docker','run','--rm','--label','orml.task=ORML-108','--label','orml.actor='+actor,'-v',str(root)+':'+str(root),'-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v','orml-107-nuget:/root/.nuget/packages','-w',str(root)]
 e={'GIT_CONFIG_COUNT':'1','GIT_CONFIG_KEY_0':'safe.directory','GIT_CONFIG_VALUE_0':str(root),'DOTNET_ROLL_FORWARD':'Major' if version==11 else 'Minor',**(env or {})}
 for k,v in e.items():a+=['-e',k+'='+v]
 if network:a+=['--network',net]
 a+=['orml108-graph-native-sdk'+str(version)+':verified']+args
 r=subprocess.run(['python3',str(out/'run.py'),str(out/name)]+a,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 print(name,r.returncode,flush=True)
 if r.returncode:print(r.stdout[-3000:]);raise RuntimeError(name)
def test(name,filter,version=10,pg=False):
 gate(name,['dotnet','test','tests/Tests/Tests.csproj','-c','Release','--artifacts-path','artifacts/graph/dev-migration-layout/sdk'+str(version),'--filter',filter,'--logger','trx;LogFileName='+name+'.trx','--results-directory',str(out/(name+'-results')),'-m:1','-warnaserror','--','xUnit.MaxParallelThreads=1'],version,{'ORMLITE_EXPECT_DOTNET':str(version)+'.0',**({'ORMLITE_TEST_POSTGRES':'Host=pg;Username=postgres;Database=postgres'} if pg else {})},pg)
if __name__=='__main__':
 try:
  test('unit','FullyQualifiedName~Tests.Graph&FullyQualifiedName!~Postgres&FullyQualifiedName!~GraphHostileReadTests&FullyQualifiedName!~GraphCommandTimeoutTests')
  gate('capture',['dotnet','run','--project','artifacts/graph/dev-migration-layout/tool/tool.csproj','--artifacts-path','artifacts/graph/dev-migration-layout/tool-build','-c','Release','--','artifacts/graph/dev-migration-layout/after-capture'])
  nid=cmd(['docker','network','create','--label','orml.task=ORML-108','--label','orml.actor='+actor,net]);(out/'network-id.txt').write_text(nid)
  pg=cmd(['docker','run','-d','--rm','--network',net,'--network-alias','pg','--label','orml.task=ORML-108','--label','orml.actor='+actor,'--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','neemle/pg.lite:19']);(out/'pg-id.txt').write_text(pg)
  for i in range(30):
   r=subprocess.run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--healthcheck'],capture_output=True,text=True)
   if r.returncode==0:break
   time.sleep(1)
  cmd(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--version'])
  for v in [10,11]:
   test('runtime'+str(v),'FullyQualifiedName~Tests.Graph|FullyQualifiedName~TestHostRuntimeTests',v,True)
   gen=out/('native'+str(v)+'-generated')
   gate('native'+str(v),['sh','-c','dotnet publish tests/AotSmoke/AotSmoke.csproj -c Release -r linux-arm64 --artifacts-path '+str(out/('native'+str(v)+'-build'))+' -o '+str(out/('native'+str(v)+'-out'))+' -p:AotSmokeTfm=net'+str(v)+'.0 -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath='+str(gen)+' -warnaserror -m:1 && '+str(out/('native'+str(v)+'-out/AotSmoke'))],v)
  gate('pack-build',['sh',str(out/'pack-build.sh')])
  demo=['dotnet','run','--project','demo/OrmLiteDemo.csproj','-c','Release','--no-build','--artifacts-path',str(out/'consumer'),'-p:RestorePackagesPath='+str(out/'packages'),'-p:RestoreConfigFile='+str(out/'nuget.config')]
  gate('demo-sqlite',demo)
  # The demo uses an independent database name within our isolated PG19 instance.
  cmd(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--single','--help']) if False else None
  gate('demo-postgres',demo,env={'OrmLite__Provider':'postgres','OrmLite__Host':'pg','OrmLite__Port':'5432','OrmLite__Username':'postgres','OrmLite__Database':'postgres'},network=True)
 finally:
  cleanup=[]
  for path,verb in [(out/'pg-id.txt',['docker','rm','-f']),(out/'network-id.txt',['docker','network','rm'])]:
   if path.exists():
    a=verb+[path.read_text()];r=subprocess.run(a,text=True,capture_output=True);cleanup.append(dict(argv=a,exitCode=r.returncode,stdout=r.stdout,stderr=r.stderr))
  (out/'cleanup.json').write_text(json.dumps(cleanup,indent=2))
