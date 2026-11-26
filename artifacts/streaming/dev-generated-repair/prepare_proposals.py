from pathlib import Path
import json,hashlib,difflib
root=Path.cwd();b=Path('artifacts/streaming/dev-generated-repair');out=b/'control-proposal';out.mkdir(parents=True,exist_ok=True)
def sha(x):return hashlib.sha256(x).hexdigest()
def keyed(path):return {(e['Group'],e['Name']):e['Text'] for e in json.loads(path.read_text())}
before=keyed(b/'before/corpus/entries.json');after=keyed(b/'verified-after/corpus/entries.json')
assert {k:v for k,v in before.items() if not k[0].startswith('generator-')}=={k:v for k,v in after.items() if not k[0].startswith('generator-')}
proposed={}
for p in (Path('artifacts/streaming/dev-lock-repair/control-proposal/tests')).rglob('*'):
 if p.is_file():proposed[str(p.relative_to('artifacts/streaming/dev-lock-repair/control-proposal'))]=p.read_bytes()
proposed['tests/Tests/Corpus/INDEX.md']=(b/'verified-after/corpus/INDEX.md').read_bytes()
p='tests/Tests/GeneratorQualityTests.cs';s=Path(p).read_text();old='''        return result.Results.SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName == "OrmLite.Entities.g.cs").SourceText.ToString();''';new='''        return string.Join("\\n", result.Results.SelectMany(r => r.GeneratedSources)
            .OrderBy(s => s.HintName, StringComparer.Ordinal).Select(s => s.SourceText.ToString()));''';assert old in s;proposed[p]=s.replace(old,new).encode()
golden='tests/Tests/Corpus/Goldens/'
for (group,name),text in after.items():
 if group.startswith('generator-'):proposed[golden+group+'/'+name+'.golden']=text.encode()
removed=[]
for (group,name),text in before.items():
 if group.startswith('generator-') and (group,name) not in after:removed.append(golden+group+'/'+name+'.golden')
records=[];patch=[]
for path,data in sorted(proposed.items()):
 p=Path(path);old=p.read_bytes() if p.exists() else b''
 if old==data:continue
 dest=out/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 records.append({'path':path,'action':'modify' if p.exists() else 'add','baseSha256':sha(old) if p.exists() else None,'proposedSha256':sha(data)})
 patch+=difflib.unified_diff(old.decode().splitlines(True),data.decode().splitlines(True),fromfile='a/'+path,tofile='b/'+path,n=0)
for path in sorted(removed):
 old=Path(path).read_bytes();records.append({'path':path,'action':'delete','baseSha256':sha(old),'proposedSha256':None});patch+=difflib.unified_diff(old.decode().splitlines(True),[],fromfile='a/'+path,tofile='b/'+path,n=0)
(out/'control-proposal.patch').write_text(''.join(patch))
manifest={'status':'UNAPPLIED / UNAPPROVED','applied':False,'approved':False,'beforeCorpusEntries':len(before),'afterCorpusEntries':len(after),'beforeGeneratorEntries':sum(k[0].startswith('generator-') for k in before),'afterGeneratorEntries':sum(k[0].startswith('generator-') for k in after),'nonGeneratorEntriesUnchanged':True,'patchFormat':'unified zero context; complete proposed files are primary review copies','allNonSourceCorpusEntriesUnchanged':645,'counts':{a:sum(r['action']==a for r in records) for a in ['modify','add','delete']},'files':records,'obligations':['Preserve GeneratorQualityTests assertions; aggregate deterministically partitioned entity hints instead of requiring a single file','Review regenerated generator golden text and exact hint inventory, no SQL/data/diagnostic semantic substitution','Retain all five prior streaming-control proposals, including typed get_StreamRowLock classification and derived current INDEX','Existing source/golden/census/quality files remain readonly; applying this proposal requires root disposition and rightful writer'],'limits':['No golden or assertion applied, no baseline approval inferred','No runtime floor/exclusion/threshold modified','Old Map full text and ALL class nontrivia tokens independently compared']}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps({k:v for k,v in manifest.items() if k!='files'},indent=2))
