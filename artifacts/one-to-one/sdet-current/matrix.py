from pathlib import Path
import json,subprocess,sys
r=Path.cwd();p=r/'artifacts/one-to-one/sdet-current'
base=json.loads((p/'docker-base.json').read_text())
images={str(i):json.loads((p/('image-'+str(i)+'.json')).read_text())['id'] for i in (10,11)}
def docker(name,version,command,provider=None,native=False):
 a=[x.replace('ORMLITE_EXPECT_DOTNET=10.0','ORMLITE_EXPECT_DOTNET='+version+'.0') for x in base]
 a+=['--ulimit','core=0','--cidfile',str(p/(name+'.cid'))]
 if native:a+=['-e','DOTNET_PROCESSOR_COUNT=1']
 if provider=='postgres':a+=['--network','5d4e72a5a54a1eb47ef2df4ff4a8ac51ddcf74e2b4c550c65397fd8d737fb3ed','-e','ONE_TO_ONE_POSTGRES=Host=pg;Username=postgres;Database=postgres']
 return [*a,images[version],*command]
def run(name,version,command,provider=None,native=False):
 subprocess.run(['python3',str(p/'run.py'),name,*docker(name,version,command,provider,native)],check=True)
def buildargs(kind,version):
 return ['examples/one-to-one/OneToOneDemo.csproj','-c','Release','--artifacts-path',str(p/(kind+'-build')),'-p:DemoTfm=net'+version+'.0','-p:OrmLiteVersion=0.1.0-dev.g25ce031','-p:CustomAfterMicrosoftCommonTargets='+str(p/'pin-source-packages.targets'),'-p:RestoreConfigFile='+str(p/'nuget.config'),'-p:RestorePackagesPath='+str(p/'packages'),'-p:UseSharedCompilation=false','-p:EmitCompilerGeneratedFiles=true','-p:CompilerGeneratedFilesOutputPath='+str(p/(kind+'-generated')),'-m:1']
if sys.argv[1]=='jit11':
 run('build-jit11-pinned','11',['dotnet','build',*buildargs('jit11','11')])
 for provider in ('sqlite','postgres'):run('jit11-'+provider,'11',['dotnet',str(p/'jit11-build/bin/OneToOneDemo/release/OneToOneDemo.dll'),provider],provider)
elif sys.argv[1] in ('native10','native11'):
 kind=sys.argv[1];version=kind[-2:]
 run('publish-'+kind,version,['dotnet','publish',*buildargs(kind,version),'-r','linux-arm64','-p:DemoNative=true','-p:IlcMaxParallelism=1','-o',str(p/(kind+'-out'))],native=True)
 for provider in ('sqlite','postgres'):run(kind+'-'+provider,version,[str(p/(kind+'-out/OneToOneDemo')),provider],provider,native=True)
else:raise ValueError('Select only remaining jit11/native10/native11; retained JIT10 is never replayed')
