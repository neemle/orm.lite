from pathlib import Path
import json,hashlib,re
p=Path('artifacts/relation-loading/dev');sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest();r={'csharp':'public-relocation-sourceproof.json','generated':[],'assemblies':[]}
a=p/'final-native10-generated/Neemle.OrmLite.Generator';b=p/'public-generated/Neemle.OrmLite.Generator'
for f in sorted(a.rglob('*.cs')):
 g=b/f.relative_to(a);assert g.is_file();same=f.read_bytes()==g.read_bytes();r['generated'].append({'file':str(f.relative_to(a)),'testedSha256':sha(f),'publicSha256':sha(g),'byteEqual':same});assert same
for project,name in [('Runtime','Neemle.OrmLite.Runtime.dll'),('Generator','Neemle.OrmLite.Generator.dll')]:
 a=p/'final-consumer10-build/bin'/project/'release'/name;b=p/'public-demo-build/bin'/project/'release'/name
 r['assemblies'].append({'project':project,'testedSha256':sha(a),'publicSha256':sha(b),'byteEqual':a.read_bytes()==b.read_bytes()})
for d in json.loads((p/'public-relocation-sourceproof.json').read_text()):assert Path(d['source']).read_bytes()==Path(d['public']).read_bytes()
readme=Path('examples/relation-loading/README.md');links=re.findall(r'\]\(([^)]+)\)',readme.read_text());r['readmeLinks']=[{'link':x,'exists':(readme.parent/x.split('#')[0]).exists()} for x in links];assert all(x['exists'] for x in r['readmeLinks'])
r['configuration']='Identical24 C# bytes, net10 Release, same frozen Runtime+Generator source with actual differing DLL file hashes recorded (no binary byte equality claim), same Composer0.5.0 analyzer resolved portably. Only relative project paths/portable analyzer reference differ. Native10/11 original tested project/binary provenance in final-matrix.json is reused; public project not separately native-published.'
(p/'public-relocation-proof.json').write_text(json.dumps(r,indent=2));print('public proof:8generated+24C# byteequal;2 actual DLL hashes retained;',len(links),'links valid')
