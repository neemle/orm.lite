from pathlib import Path
import sys
sys.path.insert(0,'artifacts/graph/dev-migration-layout')
from gates import gate,out
for label,before,after in [('native10','before-native','native10-generated'),('native11','before-native','native11-generated'),('demo','before-demo','generated')]:
 gate('tokens-'+label,['sh','-c','dotnet run --project '+str(out/'proof/proof.csproj')+' --artifacts-path '+str(out/'proof-build')+' -c Release -- '+str(out/before)+' '+str(out/after)+' > '+str(out/('tokens-'+label+'-report.json'))])
files=[Path('src/Generator/MigrationGenerator.cs'),Path('src/Generator/GraphEmitter.cs'),*Path('src/Generator/GraphLayout').glob('*.cs')]
for folder in ['after-capture/proposed','after-capture/hygiene','after-capture/collision','case-after-fixed/collision','native10-generated','native11-generated','generated']:
 files += [f for f in (out/folder).rglob('*') if f.is_file() and '.g.cs' in f.name]
(out/'all-metric-files.txt').write_text(''.join(str(f)+'\n' for f in files))
gate('metrics-all',['sh','-c','dotnet run --project artifacts/graph/dev-generated-repair/measure/measure.csproj --artifacts-path '+str(out/'metrics-build')+' -c Release -- '+str(out/'all-metric-files.txt')+' > '+str(out/'metrics-all-report.json')])
