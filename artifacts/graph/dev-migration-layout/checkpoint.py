from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET,zipfile,collections,datetime
p=Path('artifacts/graph/dev-migration-layout');H=lambda b:hashlib.sha256(b).hexdigest();G=lambda *a:subprocess.check_output(['git',*a],text=True).strip()
# Corrected probe: fixed identities retain full source bytes under enumeration reversal.
a=p/'case-after-fixed/collision/0';b=p/'case-after-fixed/collision/1';rows=[]
for f in sorted(a.glob('*.g.cs')):
 other=b/f.name;rows.append(dict(hint=f.name,sameBytes=f.read_bytes()==other.read_bytes(),sha256=H(f.read_bytes())))
assert rows and all(r['sameBytes'] for r in rows)
for i in [0,1]:
 j=json.loads((p/f'case-after-fixed/collision/{i}/diagnostics.json').read_text());assert not j['generator'] and not j['compilation']
 j=json.loads((p/f'case-before-fixed/collision/{i}/diagnostics.json').read_text());assert len(j['generator'])==1 and 'ORMLITE031' in j['generator'][0]
(p/'case-fixed-proof.json').write_text(json.dumps(dict(sourceIdentities=['0.cs=lowercase row','1.cs=uppercase Row'],permutation='reverse enumeration only; paths and texts fixed',rows=rows,beforeGeneratorSha256=H((p/'before-generator.dll').read_bytes()),beforeGeneratorCarrier='artifacts/graph/demo/pack-build-final.json (0478 source tree6cf27dc equals d28 baseline)',history='Original case-permutation-proof.json and initial-case-permutation-expectation.txt are INVALID HARNESS HISTORY: changed path/text association, no product-order finding. Superseded by this fixed-identity proof.'),indent=2))
# Actual private pack analyzer, direct generator and compiler-selected analyzer are byte-identical.
packages=[]
generator=p/'build/bin/Generator/release/Neemle.OrmLite.Generator.dll';genhash=H(generator.read_bytes())
for f in sorted((p/'feed').glob('*.nupkg')):
 with zipfile.ZipFile(f) as z:
  analyzers=[dict(entry=n,sha256=H(z.read(n)),equalsCurrentGenerator=H(z.read(n))==genhash) for n in z.namelist() if n.endswith('Neemle.OrmLite.Generator.dll')]
 packages.append(dict(path=str(f),sha256=H(f.read_bytes()),size=f.stat().st_size,analyzers=analyzers))
assert any(x['analyzers'] for x in packages) and all(a['equalsCurrentGenerator'] for x in packages for a in x['analyzers'])
selected=list((p/'packages').glob('neemle.ormlite/*/analyzers/dotnet/cs/Neemle.OrmLite.Generator.dll'));assert len(selected)==1 and H(selected[0].read_bytes())==genhash
(p/'packed-analyzer-identity.json').write_text(json.dumps(dict(generatorSha256=genhash,selectedConsumerAnalyzer=dict(path=str(selected[0]),sha256=H(selected[0].read_bytes())),packages=packages),indent=2))
# Explicitly record inherited Map outliers, preserving exact before/after full text/loci.
raw=json.loads((p/'metrics-all-report.json').read_text());records=raw['records'];maps=[]
for name in ['tokens-corpus','tokens-native10','tokens-native11','tokens-demo']:
 j=json.loads((p/(name+'-report.json')).read_text());assert j['sameKeys'] and all(x['TokensEqual'] for x in j['rows']);assert all(x['sameFlattenedStatements'] and x['unchangedModuleInitializerSignature'] for x in j['initializers']);assert all(x['TokensEqual'] and x['TextEqual'] for x in j['preGraphMapRows']);maps+=j['preGraphMapRows']
outliers=[]
for r in records:
 for m in r['methods']:
  if m['codeLines']>30:outliers.append(dict(file=r['file'],metric='methodCodeLines',value=m['codeLines'],method=m,identity=next(x for x in maps if x['AfterFile']==r['file'] and m['name']=='Map')))
 for l in r['longLines']:
  outliers.append(dict(file=r['file'],metric='width',value=l['length'],line=l['line'],identity=next(x for x in maps if x['AfterFile']==r['file'])))
assert len(outliers)==4
assert all(len(r['types'])==1 and all(t['codeLines']<=500 for t in r['types']) for r in records)
assert all(m['parameters']<=5 and m['complexity']<=10 and (m['nesting'] or {}).get('depth',0)<=4 for r in records for m in r['methods'])
(p/'metrics-disposition.json').write_text(json.dumps(dict(scope='3 actual Generator source files +164 actual generated physical files: corpus/golden,hygiene,case,native10,native11,current packed demo',raw301='FAIL retained old Map limits; NO blanket301 PASS',changedInputBoundaries='PASS actual width/method/type/CC/nesting/parameters outside identical old Map outliers',outliers=outliers,migrationWidth=dict(before=max(map(len,next((p/'before-demo').rglob('OrmLite.Migrations.g.cs')).read_text().splitlines())),after=max(map(len,next((p/'generated').rglob('OrmLite.Migrations.g.cs')).read_text().splitlines())))),indent=2))
trxs=[]
for f in sorted(p.glob('*-results/*.trx')):
 xml=ET.parse(f);c=xml.find('.//{*}Counters');trxs.append(dict(path=str(f),sha256=H(f.read_bytes()),counts=c.attrib))
native=[]
for v in [10,11]:
 f=p/f'native{v}-out/AotSmoke';native.append(dict(path=str(f),sha256=H(f.read_bytes()),size=f.stat().st_size,runtimeLog=f'native{v}.log'))
# Exact cleanup absence checks, never touch any foreign ID.
checks=[]
for argv in [['docker','inspect',(p/'pg-id.txt').read_text()],['docker','network','inspect',(p/'network-id.txt').read_text()]]:
 r=subprocess.run(argv,text=True,capture_output=True);assert r.returncode!=0;checks.append(dict(argv=argv,exitCode=r.returncode,stdout=r.stdout,stderr=r.stderr))
(p/'cleanup-absence.json').write_text(json.dumps(checks,indent=2))
gateNames=['unit','capture','runtime10','native10','runtime11','native11','pack-build','demo-sqlite','demo-postgres','case-before-fixed','case-after-fixed','tokens-corpus','metrics-corpus','tokens-native10','tokens-native11','tokens-demo','metrics-all']
gates=[]
for n in gateNames:
 j=json.loads((p/(n+'.json')).read_text());assert j['commit']==G('rev-parse','HEAD') and j['exitCode']==0 and not j['sourceChangedDuringRun'];gates.append(dict(name=n,record=n+'.json',transcript=n+'.log',**j))
proposal=json.loads((p/'proposal-manifest.json').read_text());assert proposal['applied']==False and proposal['approved']==False
for row in proposal['files']:
 f=Path(row['path']);assert (H(f.read_bytes()) if f.exists() else None)==row['beforeSha256']
 if row['proposalFile']:assert H(Path(row['proposalFile']).read_bytes())==row['proposedSha256']
assert G('diff','d28c4c1','HEAD','--','tests','demo','docs','src/Runtime','src/Sql','src/Postgres','src/Sqlite','src/Generator/EntityEmitter.cs')==''
manifest=dict(sourceCheckpoint=G('rev-parse','HEAD'),baseline='d28c4c1df8d4c869b10f1bb3c338fedf6619a381',trees={x:G('rev-parse','HEAD:'+x) for x in ['src','tests','demo','docs']},changedSource=G('diff','--name-only','d28c4c1','HEAD','--','src').splitlines(),gateCount=len(gates),gates=gates,trxs=trxs,nativeBinaries=native,proposal=dict(path='proposal-manifest.json',sha256=H((p/'proposal-manifest.json').read_bytes()),counts=proposal['counts'],applied=False,approved=False),proofs=['tokens-corpus-report.json','tokens-native10-report.json','tokens-native11-report.json','tokens-demo-report.json','case-fixed-proof.json','packed-analyzer-identity.json','metrics-all-report.json','metrics-disposition.json'],limits=['Protected105-file proposal UNAPPLIED and UNAPPROVED. No test/golden/census/control source changed.','Raw301 retains old Map31/31/47 lines and width146, exact unchanged full-text loci recorded; no global301 PASS.','Current raw coverage NOT RUN; prior raw coverage FAIL remains historical and unresolved. Full hosted CI e4338950 (3756pass/5fail/2skip each) historical, not rerun/relabelled.','No whole QA/security/release/item PASS; only this generation-input unit verified.','Initial invalid permutation and old extraction-assuming proof are retained HARNESS HISTORY; corrected attributable records current.'],generated=datetime.datetime.now(datetime.timezone.utc).isoformat(),usage=dict(tokens='UNKNOWN',monetaryCost='UNKNOWN'))
(p/'checkpoint-manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps({k:v for k,v in manifest.items() if k not in ['gates','trxs','nativeBinaries']},indent=2))
