import datetime, hashlib, json, os, pathlib, subprocess, sys, time

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = pathlib.Path(__file__).resolve().parent
OLD = ROOT / 'artifacts/graph-save/r1-demo'
CLAIM = '5c5d6c25-ef4d-405d-8e62-4d3ddd236fdc'
SDK = {10:'sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27',11:'sha256:617441f9e33eea21c352a4343ee42a50ecd9003fe5fbbe6f0cf035f2e9ff62c6'}
PG = 'orml112-public-pg-20261126'
NET = 'orml112-public-net-20261126'
PGIMAGE = 'sha256:790e4afa39efa8fcb2edf47692c5330e7a36a5e89b0fa4f872848cbe4bf70117'
VERSION = '0.1.0-dev.gfffc5b9'
STEPS = ['jit10-pg','jit11-build','jit11-sqlite','jit11-pg','native10-build','native10-sqlite','native10-pg','native11-build','native11-sqlite','native11-pg','golden-generate','golden-assert']

def now(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def write(p,x): p.write_text(json.dumps(x,indent=2)+'\n')
def call(args):
    p=subprocess.run(args,cwd=ROOT,text=True,capture_output=True)
    return {'argv':args,'exitCode':p.returncode,'stdout':p.stdout,'stderr':p.stderr}
def git(*args): return subprocess.check_output(['git',*args],cwd=ROOT,text=True).strip()
def trees(): return {k:git('rev-parse','HEAD:'+k) for k in ['src','src/Runtime','demo','tests']}
def name(step): return 'orml112-public-'+step+'-20261126'
def base(step,major,network=False,expected=None):
    args=['docker','run','--rm','--name',name(step),'--memory=3g','--cpus=2','--label','orml.task=ORML-112','--label','orml.claim='+CLAIM,'--cidfile',str(OUT/(step+'.cid')),'-v',str(ROOT)+':'+str(ROOT)+':ro','-v',str(ROOT.parents[1])+':'+str(ROOT.parents[1])+':ro','-v',str(OUT)+':'+str(OUT),'-v','orml-107-nuget:/warm:ro','-w',str(ROOT)]
    if step.startswith('golden'):
        args += ['-v',str(ROOT/'tests/Tests/OrmLiteGolden/Expected')+':'+str(ROOT/'tests/Tests/OrmLiteGolden/Expected')]
    env={'DOTNET_CLI_HOME':str(OUT/'cli'),'NUGET_PACKAGES':str(OUT/'packages'),'NUGET_HTTP_CACHE_PATH':str(OUT/'http'),'NUGET_PLUGINS_CACHE_PATH':str(OUT/'plugins'),'TMPDIR':str(OUT/'tmp'),'DOTNET_CLI_USE_MSBUILD_SERVER':'0','MSBUILDUSESERVER':'0','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_NOLOGO':'1','DOTNET_PROCESSOR_COUNT':'1','GIT_CONFIG_COUNT':'1','GIT_CONFIG_KEY_0':'safe.directory','GIT_CONFIG_VALUE_0':str(ROOT)}
    if network:
        args += ['--network',NET]
        env['ORMLITE_GRAPH_POSTGRES']='Host='+PG+';Port=5432;Username=postgres;Database=postgres;Pooling=false'
    if expected: env['ORMLITE_EXPECT_DOTNET']=str(expected)
    if step=='golden-generate': env['ORMLITE_GOLDEN_REGENERATE']='1'
    for k,v in env.items(): args += ['-e',k+'='+v]
    return args+[SDK[major]]
def execute(step,major,command,network=False,expected=None):
    args=base(step,major,network,expected)+command
    rec={'step':step,'argv':args,'cwd':str(ROOT),'start':now(),'commit':git('rev-parse','HEAD'),'treesBefore':trees()}
    write(OUT/(step+'-start.json'),rec)
    with (OUT/(step+'.log')).open('w') as f:
        p=subprocess.run(args,cwd=ROOT,stdout=f,stderr=subprocess.STDOUT)
    rec.update(exitCode=p.returncode,finish=now(),treesAfter=trees())
    cid=OUT/(step+'.cid')
    if cid.exists():
        rec['containerId']=cid.read_text().strip()
        rec['cleanupInspect']=call(['docker','container','inspect',rec['containerId']])
    write(OUT/(step+'.json'),rec)
    print(step,'exit',p.returncode,flush=True)
    if p.returncode: raise RuntimeError(step+' failed; stop without source changes')
    return rec

def setup():
    names=[name(s) for s in STEPS]+[PG]
    checks=[call(['docker','container','inspect',n]) for n in names]
    checks.append(call(['docker','network','inspect',NET]))
    write(OUT/'absence-before.json',checks)
    if any(c['exitCode']==0 for c in checks): raise RuntimeError('A fixture name already exists')
    for d in ['cli','packages','http','plugins','tmp']: (OUT/d).mkdir(exist_ok=True)
    config=(OLD/'nuget.config').read_text()
    (OUT/'nuget.config').write_text(config)
    operations=[call(['docker','network','create','--internal','--label','orml.task=ORML-112','--label','orml.claim='+CLAIM,NET])]
    if operations[-1]['exitCode']: raise RuntimeError('network create failed')
    operations.append(call(['docker','run','-d','--rm','--name',PG,'--label','orml.task=ORML-112','--label','orml.claim='+CLAIM,'--memory=1g','--cpus=1','--network',NET,'--tmpfs','/var/lib/postgresql:rw,size=512m','--cidfile',str(OUT/'pg.cid'),'-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','-e','POSTGRES_DB=postgres',PGIMAGE]))
    write(OUT/'fixture-setup.json',{'generated':now(),'operations':operations})
    if operations[-1]['exitCode']: raise RuntimeError('PG create failed')
    ready=False
    for _ in range(40):
        p=call(['docker','logs',PG]); (OUT/'pg-server.log').write_text(p['stdout']+p['stderr'])
        if 'database system is ready to accept connections' in p['stdout']+p['stderr']: ready=True; break
        time.sleep(.25)
    write(OUT/'readiness.json',{'generated':now(),'readyLog':ready,'protocol':'first actual public Npgsql chapter connection, no pg_isready/shell probe'})
    if not ready: raise RuntimeError('PG log readiness failed')

def cleanup():
    operations=[]
    if (OUT/'pg.cid').exists():
        cid=(OUT/'pg.cid').read_text().strip()
        logs=call(['docker','logs',cid]); (OUT/'pg-server.log').write_text(logs['stdout']+logs['stderr'])
        operations.append(call(['docker','stop','--time','10',cid]))
        operations.append(call(['docker','container','inspect',cid]))
    # The fixture is ours only if this process created it, as witnessed by its CID.
    if (OUT/'fixture-setup.json').exists():
        operations.append(call(['docker','network','rm',NET]))
        operations.append(call(['docker','network','inspect',NET]))
    write(OUT/'fixture-cleanup.json',{'generated':now(),'operations':operations})

def build(step,major,aot=False):
    target=OUT/(('native'+str(major)) if aot else 'jit11')
    props=['-p:OrmLiteVersion='+VERSION,'-p:RestorePackagesPath='+str(OUT/'packages'),'-p:RestoreConfigFile='+str(OUT/'nuget.config'),'-p:UseSharedCompilation=false','-p:DemoTfm=net'+str(major)+'.0','-m:1','--nologo']
    args=['dotnet','publish' if aot else 'build','demo/OrmLiteDemo.csproj','-c','Release','--artifacts-path',str(target/'build')]+props
    if aot: args += ['-r','linux-arm64','-p:PublishAot=true','-p:IlcMaxParallelism=1','-o',str(target/'publish')]
    # Copy only dependency cache into the granted output, preserving immutable old cache.
    command='set -eu\n'
    if step=='jit11-build': command+='cp -an '+str(OLD/'packages')+'/. '+str(OUT/'packages')+'/\n'
    if aot: command+='command -v clang\nclang --version\n'
    import shlex
    command+=shlex.join(args)+'\n'
    evalargs=['dotnet','msbuild','demo/OrmLiteDemo.csproj','-getItem:Compile','-getProperty:TargetFramework,RuntimeIdentifier','-p:OrmLiteVersion='+VERSION,'-p:DemoTfm=net'+str(major)+'.0','-p:RestorePackagesPath='+str(OUT/'packages'),'-p:ArtifactsPath='+str(target/'build'),'-p:UseArtifactsOutput=true']
    if aot: evalargs += ['-p:RuntimeIdentifier=linux-arm64','-p:PublishAot=true']
    command+=shlex.join(evalargs)+' > '+shlex.quote(str(OUT/(step+'-compile.json')))+'\n'
    script=OUT/(step+'.sh');script.write_text(command)
    execute(step,major,['bash',str(script)])
    return target/('publish/OrmLiteDemo' if aot else 'build/bin/OrmLiteDemo/release_net11.0/OrmLiteDemo.dll')

def main():
    try:
        setup()
        execute('jit10-pg',10,['dotnet',str(OLD/'jit10-build/bin/OrmLiteDemo/release/OrmLiteDemo.dll'),'graph-save','postgres'],True,10)
        jit=build('jit11-build',11)
        # Artifacts pivots depend on SDK conventions; select the sole current app DLL after build.
        dlls=[p for p in (OUT/'jit11/build/bin/OrmLiteDemo').rglob('OrmLiteDemo.dll')]
        if len(dlls)!=1: raise RuntimeError('not exactly one freshly built JIT11 DLL')
        jit=dlls[0]
        execute('jit11-sqlite',11,['dotnet',str(jit),'graph-save','sqlite'],False,11)
        execute('jit11-pg',11,['dotnet',str(jit),'graph-save','postgres'],True,11)
        for major in [10,11]:
            binary=build('native'+str(major)+'-build',major,True)
            execute('native'+str(major)+'-sqlite',major,[str(binary),'graph-save','sqlite'],False,major)
            execute('native'+str(major)+'-pg',major,[str(binary),'graph-save','postgres'],True,major)
    finally: cleanup()

if __name__=='__main__': main()
