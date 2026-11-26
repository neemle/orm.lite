from pathlib import Path
import json,hashlib,subprocess,xml.etree.ElementTree as ET
b=Path('artifacts/foreign-keys/dev-migration-layout')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def parse_report(name):
 s=(b/(name+'.log')).read_text();r=json.loads(s[s.index('{\n'):]);(b/(name+'-report.json')).write_text(json.dumps(r,indent=2)+'\n');return r
source=parse_report('source-metrics');generated=parse_report('generated-metrics');proof=parse_report('token-proof')
for r in [source,generated]:
 for f in r['records']:
  assert len(f['types'])==1 and not f['longLines']
  assert all(t['codeLines']<=500 for t in f['types'])
  assert all(m['codeLines']<=30 and m['parameters']<=5 and m['complexity']<=10 and (not m['nesting'] or m['nesting']['depth']<=4) for m in f['methods'])
assert len(generated['records'])==41 and proof['zeroOptInCorpusByteExact'] and proof['caseCompilationAndPermutation']
assert all(row['tokenAndNamespaceEqual'] and row['oldMapFullTextEqual'] for group in proof['groups'] for row in group['rows'])
assert (b/'before/corpus/entries.json').read_bytes()==(b/'after/corpus/entries.json').read_bytes()
versions=json.loads((b/'provider-version.log').read_text());assert versions['postgres'].startswith('19.')
current={}
for name in ['unit-final','providers10','providers11','native10','native11','pack-build','demo-sqlite','demo-postgres','after-capture','source-metrics','generated-metrics','token-proof','provider-version']:
 r=json.loads((b/(name+'.json')).read_text());assert r['exitCode']==0 and not r['sourceChangedDuringRun'];current[name]=r
trxs={}
for name,path,expected in [('unit','unit10-results/unit-final.trx',63),('runtime10','providers10-results/providers.trx',79),('runtime11','providers11-results/providers.trx',79)]:
 r=ET.parse(b/path).getroot();counts=r.find('.//{*}Counters').attrib;assert int(counts['passed'])==expected and counts['failed']=='0' and counts['notExecuted']=='0';trxs[name]={'path':str(b/path),'sha256':sha(b/path),'counts':counts}
for name in ['demo-sqlite','demo-postgres']:assert 'demo ok' in (b/(name+'.log')).read_text()
for major in ['10','11']:assert f'foreign-key native runtime: {major}.0.' in (b/('native'+major+'.log')).read_text() and 'foreign-key native ok' in (b/('native'+major+'.log')).read_text()
head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip();trees={x:subprocess.check_output(['git','rev-parse','HEAD:'+x],text=True).strip() for x in ['src','tests','demo']};assert trees['tests']=='13306f57f2988ba7d1b03a4de866db224dd10830'
assert not subprocess.check_output(['git','diff','7a5f94cd','HEAD','--','src/Runtime','src/Sql','src/Sqlite','src/Postgres','tests','demo','docs'])
old_migration=next((b/'before/demo').rglob('OrmLite.Migrations.g.cs'));new_migration=next((b/'generated').rglob('OrmLite.Migrations.g.cs'))
widths={'old':max(map(len,old_migration.read_text().splitlines())),'new':max(map(len,new_migration.read_text().splitlines()))};assert widths['old']==1363 and widths['new']<=120
manifest={'scope':'ORML-110 bounded generator migration-layout/case-hint repair, no whole-feature completion','sourceCommit':head,'trees':trees,'sourceFiles':['src/Generator/MigrationGenerator.cs','src/Generator/ForeignKeyLayout/ForeignKeySourceLayout.cs','src/Generator/ForeignKeyLayout/ForeignKeyMigrationLayout.cs'],'unchangedOwnHelper':'src/Generator/ForeignKeyLayout/ForeignKeyLineLayout.cs','source301FilesMeasured':len(source['records']),'actualGenerated301FilesMeasured':len(generated['records']),'sourceAndActualFeatureGenerated301':'PASS','fullRawCoverage':'NOT RUN on current source; old SDET raw floor remains historical FAIL and must not be relabeled current','zeroOptIn':'ALL669 corpus entries/54 generator entries byte-exact; no old output/golden/census/assertion changed','caseRegression':'before actual FK-opted-in row/Row ORMLITE031 retained; after zero compiler errors and byte-identical discovery permutation','actualProviderVersions':versions,'demoMigrationMaximumWidth':widths,'tokenAndMapComparisons':proof,'tests':trxs,'currentGates':current,'nativeBinaryHashes':{major:sha(b/('native'+major+'-out/ForeignKeysAot')) for major in ['10','11']},'limits':['No116 implementation consumed; own110 formatter reused','All runtime/providers/entities/tests/native/projects/docs/spec/ADR/controls readonly','No initializer extraction, literal/nontrivia token/metadata/SQL/version/order change','No proposals necessary: zero-opt-in corpus remains exact and opted-in hint names have no protected checked baseline in this scope','No current raw coverage/security delta/full matrix/docs/demo stage/decomposition/review/integration/merge/item/release PASS','Historical failed native artifacts and all before inputs/logs retained; no new native failure','Timing/cost counters unknown']}
(b/'checkpoint-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps({'sourceCommit':head,'trees':trees,'sourceFilesMeasured':len(source['records']),'generated':len(generated['records']),'width':widths,'classComparisons':sum(g['count'] for g in proof['groups']),'zeroOptInEntries':669},indent=2))
