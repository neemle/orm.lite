import importlib.util, pathlib, sys, hashlib, json

HERE=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('matrix',HERE/'run_matrix.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.OUT=HERE/'golden';m.OUT.mkdir(exist_ok=True)
for d in ['cli','packages','http','plugins','tmp']:(m.OUT/d).mkdir(exist_ok=True)
(m.OUT/'nuget.config').write_text((HERE/'recovery-1/nuget.config').read_text())
head=HERE/'recovery-1/frozen-public-head'
githead=m.ROOT.parents[1]/'worktrees/orml-112-graph-save/HEAD'
base=m.base
def with_freeze(step,major,network=False,expected=None):
    args=base(step,major,network,expected)
    args[-1:-1]=['-v',str(head)+':'+str(githead)+':ro']
    return args
m.base=with_freeze
mode=sys.argv[1]
step='golden-'+mode
expected=m.ROOT/'tests/Tests/OrmLiteGolden/Expected'
if mode=='generate':
    # Only the six pre-existing source pins, preserved once before intended additive changes.
    m.write(m.OUT/'before.json',{p.name:{'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'text':p.read_text()} for p in expected.glob('*.golden')})
command=['dotnet','test','tests/Tests/Tests.csproj','-c','Release','--artifacts-path',str(m.OUT/'build'),'-p:RestorePackagesPath='+str(m.OUT/'packages'),'-p:RestoreConfigFile='+str(m.OUT/'nuget.config'),'-p:UseSharedCompilation=false','-m:1','--filter','FullyQualifiedName=Neemle.OrmLite.Tests.OrmLiteGolden.OrmLiteGoldenTests.The_generated_source_of_the_non_soft_fixture_entities_matches_the_golden_byte_for_byte','--logger','trx;LogFileName='+step+'.trx','--results-directory',str(m.OUT/'results'),'--nologo']
if mode=='assert':command.append('--no-build')
import shlex
script='set -eu\n'
if mode=='generate':script+='cp -an '+str(HERE/'recovery-1/packages')+'/. '+str(m.OUT/'packages')+'/\n'
script+=shlex.join(command)+'\n'
p=m.OUT/(step+'.sh');p.write_text(script)
m.execute(step,10,['bash',str(p)])
