import pathlib,json,re,hashlib,zipfile,xml.etree.ElementTree as E,subprocess,datetime
P=pathlib.Path('artifacts/foreign-keys/current-main-integration');W=pathlib.Path.cwd()
sha=lambda value:hashlib.sha256(value).hexdigest()
a=json.loads((P/'captured-old/tokens.json').read_text());b=json.loads((P/'captured-current/tokens.json').read_text());assert a==b and len(a)==14
sources=[]
for path in sorted((P/'captured-current').glob('*.cs')):
 actual=list((P/'generated-current').rglob(path.name));assert len(actual)==1
 assert path.read_text()==actual[0].read_text(encoding='utf-8-sig'),path
 sources.append({'hint':path.name,'actual':str(actual[0].relative_to(P)),'sha256':sha(actual[0].read_bytes()),'captureTextEqualActualMsbuild':True})
packages=[]
for path in sorted((P/'feed').glob('*.nupkg')):
 with zipfile.ZipFile(path) as z:
  root=E.fromstring(z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))));get=lambda key:next(n.text for n in root.iter() if n.tag.split('}')[-1]==key)
  identity=get('id');version=get('version');assert version=='0.1.0-dev.g2f14178'
  entries=[]
  for name in z.namelist():
   if not(name.endswith(('.dll','.props','.targets')) or '/ormlite.capabilities.' in name):continue
   content=z.read(name);cache=P/'packages'/identity.lower()/version/name;assert cache.read_bytes()==content
   if name.startswith('lib/') and name.endswith('.dll'):assert (P/'demo-build/bin/ForeignKeysDemo/release'/pathlib.Path(name).name).read_bytes()==content
   entries.append({'name':name,'sha256':sha(content),'consumedCacheByteEqual':True})
  packages.append({'id':identity,'version':version,'file':str(path.relative_to(P)),'sha256':sha(path.read_bytes()),'entries':entries})
assert len(packages)==3
assets=P/'demo-build/obj/ForeignKeysDemo/project.assets.json';r=json.loads(assets.read_text());selected=sorted(k for k in r['libraries'] if k.startswith('Neemle.OrmLite'));assert selected==sorted(x['id']+'/'+x['version'] for x in packages)
old=W/'artifacts/foreign-keys/demo-parity/nuget/neemle.ormlite/0.1.0-dev.g39a20f2/analyzers/dotnet/cs/Neemle.OrmLite.Generator.dll'
current=P/'build/bin/Generator/release/Neemle.OrmLite.Generator.dll'
for which,original in [('old',old),('current',current)]:
 copies=list((P/('capture-'+which+'-build/bin')).rglob('Neemle.OrmLite.Generator.dll'));assert len(copies)==1 and copies[0].read_bytes()==original.read_bytes()
packed_analyzer=P/'packages/neemle.ormlite/0.1.0-dev.g2f14178/analyzers/dotnet/cs/Neemle.OrmLite.Generator.dll'
original=json.loads((W/'artifacts/foreign-keys/demo-parity/matrix-results.json').read_text())['legs'][0]['result'];legs=[]
for name in ['current-jit10-sqlite','current-jit10-postgres']:
 raw=(P/(name+'.log')).read_text();match=re.search(r'FOREIGN-KEY-DEMO v1\n(?:[^\n]*\n)*?assertions=47 PASS',raw);assert match and match.group(0)==original
 record=json.loads((P/(name+'.json')).read_text());assert record['exitCode']==0 and not record['demoChangedDuringRun'] and not record['commitChangedDuringRun']
 legs.append({'gate':name,'commonResult':match.group(0),'originalByteEqual':True,'fullLog':name+'.log','runtime':'10.0.15'})
counters=E.parse(P/'results/affected.trx').find('.//{*}Counters').attrib;assert counters['total']==counters['passed']=='158' and counters['failed']==counters['notExecuted']=='0'
kept=['src/Runtime/ForeignKeyBaseline.cs','src/Runtime/ForeignKeyValidation.cs','src/Runtime/ForeignKeyOrder.cs','src/Runtime/EntityDescriptor.cs','src/Runtime/EntityStatements.cs','src/Runtime/OrmExec.cs','src/Generator/ForeignKeyAnalysis.cs','src/Generator/ForeignKeyEmitter.cs','src/Generator/ForeignKeyReader.cs']
assert not subprocess.check_output(['git','diff','--name-only','39a20f28','HEAD','--',*kept],text=True).strip()
proof={'generated':datetime.datetime.now(datetime.timezone.utc).isoformat(),'executionCommit':'2f14178742d386a430dcadc4676a7d700602714a','sourceTree':'5ac963aa6b60d175ade27276ae0107fb3203aa88','testTree':'470a28bf1b507925dce5a30be6478e6293497e64','affectedCounters':counters,'oldAnalyzer':{'source':'39a20f28/currentdemo0c765b98','file':str(old),'sha256':sha(old.read_bytes())},'currentAnalyzer':{'captureInputTestsBuildFile':str(current),'captureInputSha256':sha(current.read_bytes()),'actualPackedFile':str(packed_analyzer),'actualPackedSha256':sha(packed_analyzer.read_bytes()),'separateBuildBinariesByteEqual':current.read_bytes()==packed_analyzer.read_bytes(),'semanticBridge':'Actual packed-analyzer MSBuild generated files are exact text-equal to capture output for all14 units; output token equality is established transitively, no unsupported separate-build binary identity assumption.'},'semanticComparison':{'types':sorted(a),'all14TokenArraysEqual':True,'tokens':'Roslyn RawKind + literal token.Text, trivia/hint partition excluded; every body/identifier/literal/order retained','bothGeneratedConsumerCompilationsErrors':0,'actualCurrentMsbuildSourcesEqualCapture':sources},'packages':packages,'actualAssets':{'file':str(assets.relative_to(P)),'sha256':sha(assets.read_bytes()),'selectedPrivateVersions':selected},'jitLibraryBytesEqualPackages':True,'currentDemoLegs':legs,'unchangedExecutedFkUnits':kept,'originalNativeReuse':{'carrier':'21dfe551-86a6-4012-b19b-d2c582d1ae76','acceptance':'fbb1ea6a-453f-462e-b782-65068803962e','source':'39a20f28b9d93625d07647ff00ba271f4234cd3f','reason':'Same publiccase inputs and14generated type token sequences; currentactualMSBuild outputs matchcapture; unchanged executed FK runtime/codegen units and ordinary open/query semantics, new metadata/stream paths unused;158currentaffectedcases and47currentbothproviderobservations corroborate. No new reflection or generated API/AOT risk found. Original8executionSHA retained, not relabelled current.'},'limits':'Finite affected integration acceptance only; finalhosted fullCI/security/TL/merge ROOT-owned. No rawcoverage program, no wholeitem PASS, no test/golden/source edits.'}
(P/'semantic-and-runtime-proof.json').write_text(json.dumps(proof,indent=2)+'\n');print('PROOF_COMPLETE 158tests 14types 2currentproviders 47each')
