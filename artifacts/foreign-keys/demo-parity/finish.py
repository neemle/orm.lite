from pathlib import Path
import json,re,hashlib,subprocess,datetime,zipfile,xml.etree.ElementTree as ET
p=Path('artifacts/foreign-keys/demo-parity');sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
r={'implementation':'39a20f28b9d93625d07647ff00ba271f4234cd3f','sourceTree':'c92d6736382f0df00086ea03a1fa6d9f56ff47b1','project':'examples/foreign-keys/ForeignKeysDemo.csproj','legs':[]}
for v in [10,11]:
 for mode in ['jit','aot']:
  for provider in ['sqlite','postgres']:
   name='jit'+str(v)+'-'+provider if mode=='jit' else 'native'+str(v)
   d=json.loads((p/(name+'.json')).read_text());assert d['exitCode']==0 and not d['inputChanged']
   log=(p/(name+'.log')).read_text();pattern=r'provider='+provider+r' runtime=('+str(v)+r'\.[^\n]+)\n(?:provider-option[^\n]+\n)?(FOREIGN-KEY-DEMO v1\n.*?assertions=47 PASS)'
   matches=re.findall(pattern,log,re.S);assert len(matches)==1,(name,provider)
   runtime,block=matches[0]
   if provider=='sqlite':assert 'ForeignKeys=False PRAGMA=0 orphan=admitted (unenforced)' in log
   r['legs'].append({'sdk':v,'mode':mode,'provider':provider,'runtime':runtime,'gate':name+'.json','result':block,'resultSha256':hashlib.sha256(block.encode()).hexdigest(),'logSha256':sha(p/(name+'.log')),'start':d['started'],'finish':d['finished']})
assert len({x['result'] for x in r['legs']})==1
r['normalization']='Shared9-case result blocks are byte-identical,47 assertions each. Only separate provider/runtime header, build logs and explicit SQLite-only ForeignKeys=False provider-option line excluded. That option line is retained and verified for both SQLiteJIT/AOT runtimes, never called enforced or silently compared with a nonexistent PG option.'
r['packages']=[]
for f in sorted((p/'packages').glob('*.nupkg')):
 with zipfile.ZipFile(f) as z:
  xml=ET.fromstring(z.read(next(n for n in z.namelist() if n.endswith('.nuspec'))));ns={'n':xml.tag.split('}')[0][1:]};pid=xml.find('n:metadata/n:id',ns).text;ver=xml.find('n:metadata/n:version',ns).text
  entry={'file':str(f.relative_to(p)),'sha256':sha(f),'id':pid,'version':ver,'dlls':[]}
  for name in z.namelist():
   if name.endswith('.dll'):
    cached=p/'nuget'/pid.lower()/ver.lower()/name;data=z.read(name);assert cached.is_file() and cached.read_bytes()==data
    entry['dlls'].append({'entry':name,'sha256':hashlib.sha256(data).hexdigest(),'consumedCacheByteEqual':True})
  r['packages'].append(entry)
r['binaries']=[]
for v in [10,11]:
 f=p/('native'+str(v)+'-out/ForeignKeysDemo');r['binaries'].append({'file':str(f.relative_to(p)),'bytes':f.stat().st_size,'sha256':sha(f)})
r['cleanup']=[]
for kind,file in [('container','postgres-id.txt'),('network','network-id.txt')]:
 i=(p/file).read_text().strip();args=['docker','rm','-f',i] if kind=='container' else ['docker','network','rm',i]
 x=subprocess.run(args,capture_output=True,text=True);assert x.returncode==0
 check=['docker','inspect',i] if kind=='container' else ['docker','network','inspect',i]
 y=subprocess.run(check,capture_output=True,text=True);assert y.returncode!=0
 r['cleanup'].append({'kind':kind,'id':i,'argv':args,'exitCode':x.returncode,'stdout':x.stdout,'absenceArgv':check,'absenceExitCode':y.returncode,'absenceStderr':y.stderr,'finished':datetime.datetime.now(datetime.timezone.utc).isoformat()})
r['limits']=['Only new publicdemo and currentmergedpackage functionality, not wholeitem/QA/coverage/security/style/merge/release approval.','All product source/oldtests/AOTconsumer/controls/oldartifacts readonly in this phase. No old89cases or expected-output regeneration.','No existing golden mismatch assumed or tested; inherited three main golden changes belong separate SDET disposition.','Sharedscenarios include real combined FK+CalculatedLower keyed/reference and typed linked calculated read.','Selfcycle/nonnullableSetNull refused; other unsupported shapes remain documented, no new support.','Explicit SQLiteForeignKeys=False observation is provider-specific and explicitlyunenforced.','Localcommitonly; no push untilROOT finalintegration disposition.']
(p/'matrix-results.json').write_text(json.dumps(r,indent=2));print('8byte-identical sharedblocks47PASS;3privatepackages;2ownresources absent')
