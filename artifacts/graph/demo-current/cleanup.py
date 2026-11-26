import datetime
import json
import pathlib
import subprocess

P = pathlib.Path('artifacts/graph/demo-current')
fixture = json.loads((P / 'fixture.json').read_text())
records = []

def run(argv):
    result = subprocess.run(argv, text=True, capture_output=True)
    records.append({'argv': argv, 'exitCode': result.returncode, 'stdout': result.stdout,
                    'stderr': result.stderr, 'at': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    return result

assert json.loads((P / 'native11-postgres.json').read_text())['exitCode'] == 0
pg = fixture['containerId']
network = fixture['networkId']
inspected = run(['docker', 'inspect', pg])
assert inspected.returncode == 0
container = json.loads(inspected.stdout)[0]
assert container['Id'] == pg
assert container['Config']['Labels']['orml.task'] == 'ORML-108'
assert container['Config']['Labels']['orml.actor'] == 'graph-demo-current'
net = run(['docker', 'network', 'inspect', network])
assert net.returncode == 0
net_data = json.loads(net.stdout)[0]
assert net_data['Id'] == network and net_data['Labels']['orml.task'] == 'ORML-108'
assert net_data['Labels']['orml.actor'] == 'graph-demo-current'
assert set(net_data['Containers']) == {pg}, net_data['Containers']
assert run(['docker', 'rm', '-f', pg]).returncode == 0
assert run(['docker', 'network', 'rm', network]).returncode == 0
assert run(['docker', 'inspect', pg]).returncode == 1
assert run(['docker', 'network', 'inspect', network]).returncode == 1
ids = [path.read_text().strip() for path in sorted(P.glob('*.cid'))]
assert len(ids) == 15
for identity in ids:
    assert run(['docker', 'inspect', identity]).returncode == 1
assert not list((P / 'tmp').glob('graph-chapter-*'))
roster = run(['docker', 'ps', '-aq', '--filter', 'label=orml.task=ORML-108',
              '--filter', 'label=orml.actor=graph-demo-current'])
assert roster.returncode == 0 and not roster.stdout.strip()
(P / 'cleanup.json').write_text(json.dumps({'generated': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'records': records, 'ownedFixtureContainer': pg, 'ownedNetwork': network,
    'all15JobContainersAbsent': ids, 'scratchDirectoriesAbsent': True, 'ownActorRosterEmpty': True,
    'foreignResourcesUntouched': True, 'sessionObservation': 'Session40013 is not portable across agents: write_stdin unknown process. No stop/restart/rerun; actual15 completed command JSON exits and original matrix.py process absence used.'}, indent=2) + '\n')
print('CLEANUP_COMPLETE', pg, network, len(ids))
