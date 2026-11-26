import pathlib,json,subprocess,sys
P=pathlib.Path('artifacts/foreign-keys/current-main-integration').resolve();base=json.loads((P/'docker-base.json').read_text());W=pathlib.Path.cwd()
for which,assembly in [('old',W/'artifacts/foreign-keys/demo-parity/nuget/neemle.ormlite/0.1.0-dev.g39a20f2/analyzers/dotnet/cs/Neemle.OrmLite.Generator.dll'),('current',P/'build/bin/Generator/release/Neemle.OrmLite.Generator.dll')]:
 argv=base+['sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27','dotnet','run','--project',str(P/'semantic-capture/Capture.csproj'),'-c','Release','--artifacts-path',str(P/('capture-'+which+'-build')),'-p:GeneratorAssemblyPath='+str(assembly),'-p:RestoreConfigFile='+str(P/'nuget.config'),'-p:UseSharedCompilation=false','--',str(P/'build/bin/Tests/release'),str(P/('captured-'+which))]
 code=subprocess.call(['python3',str(P/'run.py'),'capture-'+which+'-semantics',*argv])
 if code:sys.exit(code)
