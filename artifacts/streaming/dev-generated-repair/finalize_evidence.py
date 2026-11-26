from pathlib import Path
import json,hashlib,subprocess,xml.etree.ElementTree as ET
base=Path('artifacts/streaming/dev-generated-repair')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def report(name,out):
 s=(base/(name+'.log')).read_text();r=json.loads(s[s.index('{\n'):]);(base/out).write_text(json.dumps(r,indent=2)+'\n');return r
old=report('before-generated-metrics','before-generated-metrics-report.json')
current=report('verified-generated-metrics','verified-generated-metrics-report.json')
sources=report('verified-source-metrics','verified-source-metrics-report.json')
oldmaps={}
for f in old['records']:
 for m in f['methods']:
  if m['name']=='Map':
   name=Path(f['file']).parent.name
   oldmaps[name+':'+next(t['name'] for t in f['types'] if t['line']<=m['start']<=t['end'])]=(f,m)
residual=[]
for f in current['records']:
 for m in f['methods']:
  issues=[]
  if m['codeLines']>30:issues.append('method length')
  lines=[x for x in f['longLines'] if m['start']<=x['line']<=m['end']]
  if lines:issues.append('line width')
  if not issues:continue
  assert m['name']=='Map'
  key=Path(f['file']).parent.name+':'+f['types'][0]['name'];before,bm=oldmaps[key]
  residual.append({'unit':key,'classification':'PREEXISTING OUTSIDE CHANGED MAPPING; FULL MAP TEXT PRESERVED','beforeFile':before['file'],'before':bm,'afterFile':f['file'],'after':m,'longLines':lines,'issues':issues})
(base/'legacy-map-loci.json').write_text(json.dumps(residual,indent=2)+'\n')
for f in current['records']:
 assert len(f['types'])<=1
 assert all(t['codeLines']<=500 for t in f['types'])
 assert all(m['parameters']<=5 and m['complexity']<=10 and (not m['nesting'] or m['nesting']['depth']<=4) for m in f['methods'])
 assert all(any(r['afterFile']==f['file'] and r['after']['start']<=line['line']<=r['after']['end'] for r in residual) for line in f['longLines'])
for f in sources['records']:
 assert len(f['types'])==1 and not f['longLines'] and f['types'][0]['codeLines']<=500
 assert all(m['codeLines']<=30 and m['parameters']<=5 and m['complexity']<=10 and (not m['nesting'] or m['nesting']['depth']<=4) for m in f['methods'])
assert len(residual)==2
trs={}
for name,path in [('unit','unit-results/verified-unit.trx'),('runtime10','verified-scoped10-results/scoped10.trx'),('runtime11','verified-scoped11-results/scoped11.trx')]:
 r=ET.parse(base/path).getroot();counter=r.find('.//{*}Counters').attrib;trs[name]={'path':str(base/path),'sha256':sha(base/path),'counts':counter};assert counter['failed']=='0'

gates={}
for name in ['verified-unit','verified-scoped10','verified-scoped11','verified-native10','verified-native11','verified-capture','verified-case-hint-probe','verified-provider-version','verified-source-metrics','verified-generated-metrics','verified-token-proof']:
 r=json.loads((base/(name+'.json')).read_text());assert r['exitCode']==0 and not r['sourceChangedDuringRun'];gates[name]=r
src=subprocess.check_output(['git','rev-parse','HEAD:src'],text=True).strip();tests=subprocess.check_output(['git','rev-parse','HEAD:tests'],text=True).strip()
assert tests=='3c0eae027bee12a914364e20657ebb8ca513aa57'
assert not subprocess.check_output(['git','diff','ed2d62a','HEAD','--','src/Runtime','src/Sql','src/Postgres','src/Sqlite','tests'])
proposal=json.loads((base/'control-proposal/manifest.json').read_text())
summary={'sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'sourceTree':src,'testTree':tests,'newSourceFilesMeasured':len(sources['records']),'source301':'PASS for four claimed Generator source files','generatedFilesMeasured':len(current['records']),'nativeConsumer301':'PASS all actual generated native10/native11 files','legacyMapResiduals':residual,'noGlobal301Pass':True,'initializer':'No extraction; exact class tokens, signatures, attributes and registration order preserved','caseHintRegression':'before ORMLITE031 captured; current compile and discovery permutation proof PASS','tests':trs,'currentGates':gates,'proposal':{k:v for k,v in proposal.items() if k!='files'},'binaryHashes':{major:sha(base/('verified-native'+major+'-out/Neemle.OrmLite.StreamingAot')) for major in ['10','11']},'limits':['No old assertions, goldens, censuses or proposals applied or approved','No full matrix, raw100% coverage, security delta, docs/demo, review, decomposition, integration, merge, release or item PASS','Earlier intermediate source gates and exploratory test-project capture attempts are HISTORY; Tests references Generator as ordinary assembly, not analyzer','No literal/nontrivia token changes or private initializer extraction; preserved legacy Map violations recorded with exact old/new loci']}
tokens=json.loads((base/'verified-token-proof-report.json').read_text())
assert all(f['nonTriviaTokensEqual'] for g in tokens for f in g['facts'])
def corpus(path):return {(e['Group'],e['Name']):e['Text'] for e in json.loads(path.read_text())}
a=corpus(base/'before/corpus/entries.json');z=corpus(base/'verified-after/corpus/entries.json')
semantic={k:v for k,v in a.items() if not k[1].endswith('.g.cs')}
assert semantic=={k:v for k,v in z.items() if not k[1].endswith('.g.cs')}
provider=json.loads((base/'verified-provider-version.log').read_text())
assert provider['postgresVersion'].startswith('19.')
assert json.loads((base/'cleanup-final.json').read_text())['containerAbsent']
summary.update({'allClassNonTriviaTokenEquality':sum(len(g['facts']) for g in tokens),
 'allNonSourceCorpusEntriesUnchanged':len(semantic),'actualProviderVersions':provider,
 'artifactOnlyAssertions':'No generated-source filename or layout control applied; exact namespace/type/method/field/body tokens preserved'})
(base/'checkpoint-manifest.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps({'sourceCommit':summary['sourceCommit'],'sourceTree':src,'testTree':tests,'generatedFiles':len(current['records']),'legacyMaps':len(residual),'proposalCounts':proposal['counts'],'tests':{k:v['counts'] for k,v in trs.items()}},indent=2))
