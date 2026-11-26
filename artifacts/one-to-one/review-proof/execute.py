from pathlib import Path
import json,subprocess,shutil,zipfile,hashlib
r=Path.cwd();p=r/'artifacts/one-to-one/review-proof'
assert json.loads((p/'compile-corrected.json').read_text())['exitCode']==0
built=p/'build/bin/Tests/release';old=p/'old-host';assert not old.exists()
shutil.copytree(built,old)
archive=r/json.loads((p/'old-runtime-provenance.json').read_text())['archive']
with zipfile.ZipFile(archive) as z:(old/'Neemle.OrmLite.Runtime.dll').write_bytes(z.read('lib/net10.0/Neemle.OrmLite.Runtime.dll'))
changed=[str(f.relative_to(built)) for f in built.rglob('*') if f.is_file() and f.read_bytes()!=(old/f.relative_to(built)).read_bytes()]
assert changed==['Neemle.OrmLite.Runtime.dll'],changed
(p/'host-bridge.json').write_text(json.dumps(dict(changedFiles=changed,fixedRuntimeSha256=hashlib.sha256((built/'Neemle.OrmLite.Runtime.dll').read_bytes()).hexdigest(),oldRuntimeSha256=hashlib.sha256((old/'Neemle.OrmLite.Runtime.dll').read_bytes()).hexdigest()),indent=2)+'\n')
base=json.loads((p/'docker-base.json').read_text())
for name,host,filter in [('old-red',old,'FullyQualifiedName~OneToOneReviewLinkNameTests'),('fixed-new-cases',built,'FullyQualifiedName~OneToOneReviewLinkNameTests|FullyQualifiedName~OneToOneReviewSecurityTests')]:
 cmd=base+['--cidfile',str(p/(name+'.cid')),'sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27','dotnet','test',str(host/'Neemle.OrmLite.Tests.dll'),'--filter',filter,'--logger','trx;LogFileName='+name+'.trx','--results-directory',str(p/'results')]
 code=subprocess.run(['python3',str(p/'run.py'),name,*cmd]).returncode
 if name=='old-red':assert code==1,code
 elif code:raise SystemExit(code)
