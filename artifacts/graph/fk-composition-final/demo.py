import pathlib,json,subprocess,sys
P=pathlib.Path('artifacts/graph/fk-composition-final').resolve();base=json.loads((P/'docker-base.json').read_text());f=json.loads((P/'fixture.json').read_text());image='sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27'
def gate(name,a):
 code=subprocess.call(['python3',str(P/'run.py'),name,*a])
 if code:sys.exit(code)
gate('build-packed-default-demo',base+[image,'dotnet','build','demo/OrmLiteDemo.csproj','-c','Release','--artifacts-path',str(P/'demo-build'),'-p:RestorePackagesPath='+str(P/'packages'),'-p:RestoreConfigFile='+str(P/'nuget.config'),'-p:UseSharedCompilation=false','-p:EmitCompilerGeneratedFiles=true','-p:CompilerGeneratedFilesOutputPath='+str(P/'generated'),'-m:1'])
for provider in ['sqlite','postgres']:
 a=base+['-e','OrmLite__Provider='+provider]
 if provider=='postgres':a+=['--network',f['networkId'],'-e','OrmLite__ConnectionString=Host=graph-pg;Username=postgres;Database=postgres;Pooling=false']
 a+=[image,'dotnet',str(P/'demo-build/bin/OrmLiteDemo/release/OrmLiteDemo.dll')]
 gate('default-jit10-'+provider,a)
