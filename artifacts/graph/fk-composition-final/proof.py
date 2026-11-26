from pathlib import Path
import json,hashlib,zipfile,xml.etree.ElementTree as E,datetime,re,subprocess
P=Path('artifacts/graph/fk-composition-final');sha=lambda b:hashlib.sha256(b).hexdigest()
bridge=json.loads((P/'graph-generated-bridge.json').read_text());assert len(bridge)==3
original=json.loads(Path('artifacts/graph/demo-current/parity-provenance.json').read_text())
for row in bridge:
 matches=[x for x in original['generatedGraphSources'] if x['sdk']=='10' and x['entity']+'Orm'==row['Type']];assert len(matches)==1
 assert row['OldSha256']==matches[0]['sha256'] and row['TokensEqual'] and row['MapTextEqual']
counters=E.parse(P/'results/composition.trx').find('.//{*}Counters').attrib;assert counters['total']==counters['passed']=='10' and counters['failed']==counters['notExecuted']=='0'
legs=[]
for provider in ['sqlite','postgres']:
 name='default-jit10-'+provider;lines=(P/(name+'.log')).read_text().splitlines();assert lines[-1]=='demo ok'
 start=next(i for i,line in enumerate(lines) if line.startswith('16a. requested graph:'))
 block=lines[start:start+8];assert block[5].startswith('16a. generated foreign key:') and not any('False' in x for x in block)
 assert block[:5]==original['legs'][0]['result'].splitlines()[:5]
 record=json.loads((P/(name+'.json')).read_text());assert record['exitCode']==0 and not record['inputsChangedDuringRun']
 legs.append({'provider':provider,'gate':name,'exitCode':0,'runtime':'10.0.15','graphAndFkBlock':'\n'.join(block)+'\n','wholeDefaultDemoOk':True})
assert legs[0]['graphAndFkBlock']==legs[1]['graphAndFkBlock']
packages=[]
for path in sorted((P/'feed').glob('*.nupkg')):
 with zipfile.ZipFile(path) as z:
  root=E.fromstring(z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))));get=lambda key:next(n.text for n in root.iter() if n.tag.split('}')[-1]==key)
  identity=get('id');version=get('version');assert version=='0.1.0-dev.g60e8d91';entries=[]
  for name in z.namelist():
   if not(name.endswith(('.dll','.props','.targets')) or '/ormlite.capabilities.' in name):continue
   data=z.read(name);assert data==(P/'packages'/identity.lower()/version/name).read_bytes()
   if name.startswith('lib/') and name.endswith('.dll'):assert data==(P/'demo-build/bin/OrmLiteDemo/release'/Path(name).name).read_bytes()
   entries.append({'entry':name,'sha256':sha(data),'consumedCacheByteEqual':True})
  packages.append({'id':identity,'version':version,'file':str(path.relative_to(P)),'sha256':sha(path.read_bytes()),'entries':entries})
assert len(packages)==3
assets=P/'demo-build/obj/OrmLiteDemo/project.assets.json';parsed=json.loads(assets.read_text());selected=sorted(k for k in parsed['libraries'] if k.startswith('Neemle.OrmLite'));assert selected==sorted(p['id']+'/'+p['version'] for p in packages)
assert not subprocess.check_output(['git','diff','--name-only','691c634f','HEAD','--','src','demo','docs'],text=True).strip()
proof={'generated':datetime.datetime.now(datetime.timezone.utc).isoformat(),'frozenProduct':'691c634f69996f14b19e2e29854505c051954be0','sourceTree':'1945c1c0407fce8a978a75834c87fc582598e4ee','executionCommit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compositionCases':counters,'currentDefaultDemo':legs,'comparisonScope':'Eight Graph+FK result lines equal acrossproviders; five Graph lines exactoriginal8mode sample. Intrinsic otherdefaultchapters provider/actor/timing outputs are retained, not normalized into a whole-output parity claim. Fulldefaultprogram returns0/demo ok.','graphGeneratedBridge':bridge,'packages':packages,'actualAssets':{'file':str(assets.relative_to(P)),'sha256':sha(assets.read_bytes()),'privatePackages':selected},'jitOutputLibraryBytesEqualPackages':True,'originalEvidence':{'Graph8':'85de623e actual2ad9d966, unchanged threeGraphclasssemantic tokens/Map andoriginalchapteroracles, reused noNative replay','FK8':'21dfe551 actual39a20f28+0c765b98/47assertions each, originalattribution retained; currentnew typedGraph FKmetadata/registration tested bythree independentcases/defaultdemo, no newNative-specific mechanism','contracts102':'b006fa57 at24b+3739c19, no blanketrerun/expectedrefresh'},'limits':'Finite Graph+FKcomposition acceptance; currentCI/TL/802/merge ROOT next. No newNative8/fullunit/coverage/census, no wholeitemPASS.'}
(P/'composition-proof.json').write_text(json.dumps(proof,indent=2)+'\n');print('COMPOSITION_PROOF_PASS 10cases 2defaultproviders 3graphTokens')
