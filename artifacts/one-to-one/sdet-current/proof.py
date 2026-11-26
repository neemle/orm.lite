from pathlib import Path
import json,hashlib,zipfile,subprocess,datetime,xml.etree.ElementTree as ET
r=Path.cwd();p=r/'artifacts/one-to-one/sdet-current'
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
def load(f):return json.loads(f.read_text())
oldfile=r/'artifacts/one-to-one/source-case-checkpoint.json';assert sha(oldfile)=='e6fd505504da1378ee3b82f2a88e0d36bfa3f7b1fcd442ab98845db7297ea118';old=load(oldfile)
legs=[]
for mode,v in [('jit',10),('jit',11),('native',10),('native',11)]:
 for provider,total in [('sqlite',55),('postgres',54)]:
  retained=mode=='jit' and v==10
  gate=r/'artifacts/one-to-one'/('jit10-'+provider+'-author-7.json') if retained else p/(mode+str(v)+'-'+provider+'.json')
  x=load(gate);assert x['exitCode']==0 and not x.get('inputChanged',False)
  log=gate.with_suffix('.log');lines=log.read_text().splitlines();common=[l for l in lines if any(l.startswith(c.split('=',1)[0]+'=') for c in old['commonCases'])]
  assert common==old['commonCases'] and 'assertions='+str(total)+' PASS' in lines
  assert lines[0].startswith('provider='+provider+' runtime='+str(v)+'.')
  legs.append(dict(mode=mode,runtime=v,provider=provider,retainedOriginal=retained,gate=str(gate.relative_to(r)),log=str(log.relative_to(r)),logSha256=sha(log),common13Exact=True,printedAssertions=total,cleanup='Final post-print scratch cleanup Require passed via process exit0',exitCode=0))
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};trx=p/'results/independent.trx';c=ET.parse(trx).find('.//t:Counters',ns).attrib;assert c['total']=='19' and c['passed']=='19' and c['failed']=='0'
packages=[]
for package in old['packages']:
 f=r/package['path'];assert sha(f)==package['sha256']
 with zipfile.ZipFile(f) as z:
  entries=[]
  for name in z.namelist():
   if (name.startswith(('lib/','analyzers/')) and name.endswith('.dll')) or name.startswith('buildTransitive/'):
    resolved=p/'packages'/f.stem.rsplit('.0.1.0',1)[0].lower()/'0.1.0-dev.g25ce031'/name
    h=hashlib.sha256(z.read(name)).hexdigest();assert resolved.is_file() and sha(resolved)==h
    entries.append(dict(entry=name,resolvedPath=str(resolved.relative_to(r)),sha256=h))
 packages.append(dict(file=package['path'],sha256=sha(f),resolvedEntries=entries))
artifacts=[];assets=[]
for kind in ['jit11','native10','native11']:
 f=p/(kind+'-build/obj/OneToOneDemo/project.assets.json');a=load(f);libs=sorted(k for k in a['libraries'] if k.startswith('Neemle.'));assert libs==sorted(old['resolvedLibraryKeys']);assets.append(dict(kind=kind,file=str(f.relative_to(r)),sha256=sha(f),resolvedLibraries=libs))
 if kind=='jit11':f=p/(kind+'-build/bin/OneToOneDemo/release/OneToOneDemo.dll')
 else:f=p/(kind+'-out/OneToOneDemo')
 artifacts.append(dict(kind=kind,file=str(f.relative_to(r)),bytes=f.stat().st_size,sha256=sha(f)))
generated=[]
def gens(folder):return {str(f.relative_to(folder)):sha(f) for f in folder.rglob('*.cs') if 'Neemle.OrmLite.Generator' in str(f)}
before=gens(r/'artifacts/one-to-one/demo/author7-generated')
for kind in ['jit11','native10','native11']:
 actual=gens(p/(kind+'-generated'));assert actual==before;generated.append(dict(kind=kind,exactOriginalGeneratedSources=True,files=actual))
assert subprocess.check_output(['git','diff','25ce031d','--','src','examples','demo','docs'],text=True)==''
result=dict(generated=datetime.datetime.now(datetime.timezone.utc).isoformat(),disposition='Finite SDET tests+remaining SAME public matrix PASS; docs/security/currentCI/TL remain owning work',productCommit=old['commit'],srcTree=old['srcTree'],workspaceHead=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),privateVersion=old['packageVersion'],retainedSourceCheckpoint=str(oldfile.relative_to(r)),retainedSourceCheckpointSha256=sha(oldfile),canonicalDev='81e2a305-80ed-47fd-ab19-a31c446cccea',newTests=dict(counters=c,trx=str(trx.relative_to(r)),sha256=sha(trx)),matrix=legs,packages=packages,assets=assets,binaries=artifacts,generatedBridge=generated,normalization='Only runtime/provider heading and separately documented55SQLite/54PG printed total/extraPRAGMA environment witness differ; compare13 namedCOMMONlines exactly, no key/value replacement and no false whole-stdout parity.',limits='JIT10 actual25ce reused unchanged;19 tests actuale2dcb99 compiled same new-file hashes from precommit25ce build. No repack/publiccase/source edit/oldoracle change; injected cleanup faults are injection, native missing-table case actual driver. No rawcoverage/census/fullsuite/oldGraphFKmatrix. SEC01 hostileidentifier/privacy breadth and compatibilityCI/docs/review not whole-accepted here.')
(p/'matrix-provenance.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(tests=c['passed'],outcomes=len(legs),common13Exact=True,generatedBridge=True,privatePackages=len(packages))))
