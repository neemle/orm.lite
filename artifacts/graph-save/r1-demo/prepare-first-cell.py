from pathlib import Path
import subprocess,json,datetime
W=Path.cwd();P=W/'artifacts/graph-save/r1-demo'
image='sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27'
base=['docker','run','--rm','--memory=3g','--cpus=2','--label','orml.task=ORML-112','--label','orml.claim=49954949-6530-466f-b8c3-773f3dedb5a2','-v',str(W.parents[1])+':'+str(W.parents[1])+':ro','-v',str(W)+':'+str(W),'-v','orml-107-nuget:/warm:ro','-w',str(W)]
for k,v in {'GIT_CONFIG_COUNT':'1','GIT_CONFIG_KEY_0':'safe.directory','GIT_CONFIG_VALUE_0':str(W),'DOTNET_CLI_HOME':str(P/'cli'),'NUGET_PACKAGES':str(P/'packages'),'NUGET_HTTP_CACHE_PATH':str(P/'http'),'NUGET_PLUGINS_CACHE_PATH':str(P/'plugins'),'TMPDIR':str(P/'tmp'),'DOTNET_CLI_USE_MSBUILD_SERVER':'0','MSBUILDUSESERVER':'0','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_NOLOGO':'1'}.items():base+=['-e',k+'='+v]
(P/'docker-base.json').write_text(json.dumps(base,indent=2)+'\n')
props=['-p:RestorePackagesPath='+str(P/'packages'),'-p:RestoreConfigFile='+str(P/'nuget.config'),'-p:UseSharedCompilation=false']
git=lambda *a:subprocess.check_output(['git',*a],text=True).strip()
def run(name,command,extra=[]):
    argv=base+['--name','orml112-'+name+'-20261126','--cidfile',str(P/(name+'.cid'))]+extra+[image]+command
    r={'argv':argv,'cwd':str(W),'start':datetime.datetime.now(datetime.timezone.utc).isoformat(),'commit':git('rev-parse','HEAD'),'sourceBefore':git('rev-parse','HEAD:src'),'demoBefore':git('rev-parse','HEAD:demo'),'runtimeBefore':git('rev-parse','HEAD:src/Runtime'),'testsBefore':git('rev-parse','HEAD:tests')}
    (P/(name+'-start.json')).write_text(json.dumps(r,indent=2)+'\n')
    with (P/(name+'.log')).open('w') as output:result=subprocess.run(argv,stdout=output,stderr=subprocess.STDOUT)
    r.update(exitCode=result.returncode,finish=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceAfter=git('rev-parse','HEAD:src'),demoAfter=git('rev-parse','HEAD:demo'),runtimeAfter=git('rev-parse','HEAD:src/Runtime'),testsAfter=git('rev-parse','HEAD:tests'))
    if (P/(name+'.cid')).exists():
        r['containerId']=(P/(name+'.cid')).read_text().strip();q=subprocess.run(['docker','inspect',r['containerId']],capture_output=True,text=True);r.update(cleanupInspectExit=q.returncode,cleanupInspectStderr=q.stderr)
    (P/(name+'.json')).write_text(json.dumps(r,indent=2)+'\n')
    print(name+' exit='+str(result.returncode),flush=True)
    if result.returncode:print((P/(name+'.log')).read_text()[-5500:],flush=True);raise SystemExit(result.returncode)
for project in ['Core','Sqlite','Postgres']:
    run('pack-'+project,['dotnet','pack','src/'+project+'/'+project+'.csproj','-c','Release','--artifacts-path',str(P/'pack-build'),'-o',str(P/'feed'),*props,'-m:1','--nologo'])
run('build-jit10',['dotnet','build','demo/OrmLiteDemo.csproj','-c','Release','--artifacts-path',str(P/'jit10-build'),*props,'-p:EmitCompilerGeneratedFiles=true','-p:CompilerGeneratedFilesOutputPath='+str(P/'generated10'),'-m:1','--nologo'])
run('jit10-sqlite',['dotnet',str(P/'jit10-build/bin/OrmLiteDemo/release/OrmLiteDemo.dll'),'graph-save','sqlite'],['-e','ORMLITE_EXPECT_DOTNET=10'])
print((P/'jit10-sqlite.log').read_text(),flush=True)
