import pathlib,json,subprocess
W=pathlib.Path.cwd();P=W/'artifacts/graph/demo-current';base=json.loads((P/'docker-base.json').read_text());fixture=json.loads((P/'fixture.json').read_text());images={'10':'sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27','11':'sha256:617441f9e33eea21c352a4343ee42a50ecd9003fe5fbbe6f0cf035f2e9ff62c6'};props=['-p:RestorePackagesPath='+str(P/'packages'),'-p:RestoreConfigFile='+str(P/'nuget.config'),'-p:UseSharedCompilation=false']
def gate(name,a):
 r=subprocess.run(['python3',str(P/'run.py'),name,*a]);assert r.returncode==0,name
def runtime(name,major,binary,provider,native):
 a=base+['--cidfile',str(P/(name+'.cid')),'-e','ORMLITE_EXPECT_DOTNET='+major]
 if provider=='postgres':a+=['--network',fixture['networkId'],'-e','ORMLITE_GRAPH_POSTGRES=Host=graph-pg;Username=postgres;Database=postgres']
 a+=[images[major]]+([] if native else ['dotnet'])+[str(binary),'graph',provider];gate(name,a)
for major in ['10','11']:
 if major=='11':gate('build-jit11',base+['--cidfile',str(P/'build-jit11.cid'),images[major],'dotnet','build','demo/OrmLiteDemo.csproj','-c','Release','-p:DemoTfm=net11.0','--artifacts-path',str(P/'jit11-build'),*props,'-p:EmitCompilerGeneratedFiles=true','-p:CompilerGeneratedFilesOutputPath='+str(P/'generated11'),'-m:1'])
 for provider in ['sqlite','postgres']:runtime('jit'+major+'-'+provider,major,P/('jit'+major+'-build/bin/OrmLiteDemo/release/OrmLiteDemo.dll'),provider,False)
print('JIT_FOUR_LEGS_FINISHED',flush=True)
for major in ['10','11']:
 gate('publish-native'+major,base+['--cidfile',str(P/('publish-native'+major+'.cid')),'-e','DOTNET_PROCESSOR_COUNT=1',images[major],'dotnet','publish','demo/OrmLiteDemo.csproj','-c','Release','-r','linux-arm64','-p:DemoTfm=net'+major+'.0','-p:PublishAot=true','-p:IlcMaxParallelism=1','--artifacts-path',str(P/('native'+major+'-build')),'-o',str(P/('native'+major+'-out')),*props,'-m:1'])
 print('NATIVE_PUBLICATION_FINISHED_'+major,flush=True)
 for provider in ['sqlite','postgres']:runtime('native'+major+'-'+provider,major,P/('native'+major+'-out/OrmLiteDemo'),provider,True)
print('ALL_EIGHT_GRAPH_CHAPTER_LEGS_FINISHED',flush=True)
