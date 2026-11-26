import pathlib,json,subprocess,sys
P=pathlib.Path('artifacts/foreign-keys/current-main-integration').resolve();base=json.loads((P/'docker-base.json').read_text());fixture=json.loads((P/'fixture.json').read_text());image='sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27'
def gate(name,argv):
 code=subprocess.call(['python3',str(P/'run.py'),name,*argv])
 if code:sys.exit(code)
gate('build-current-public-demo',base+[image,'dotnet','build','examples/foreign-keys/ForeignKeysDemo.csproj','-c','Release','--artifacts-path',str(P/'demo-build'),'-p:RestorePackagesPath='+str(P/'packages'),'-p:RestoreConfigFile='+str(P/'nuget.config'),'-p:UseSharedCompilation=false','-p:EmitCompilerGeneratedFiles=true','-p:CompilerGeneratedFilesOutputPath='+str(P/'generated-current'),'-m:1'])
for provider in ['sqlite','postgres']:
 argv=base+['--network',fixture['networkId'],'-e','FOREIGN_KEYS_POSTGRES=Host=fk-pg;Username=postgres;Database=postgres',image,'dotnet',str(P/'demo-build/bin/ForeignKeysDemo/release/ForeignKeysDemo.dll'),provider]
 gate('current-jit10-'+provider,argv)
