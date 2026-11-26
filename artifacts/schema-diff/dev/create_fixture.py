import datetime
import json
import pathlib
import subprocess
import sys


def run(argv):
    result = subprocess.run(argv, text=True, capture_output=True)
    if result.returncode != 0:
        raise RuntimeError(result.stderr)
    return result.stdout.strip()


def main():
    root = pathlib.Path('artifacts/schema-diff/dev')
    name = sys.argv[1]
    network = run(['docker', 'network', 'create', '--label', 'orml.task=ORML-117',
                   '--label', 'orml.actor=dev', name])
    argv = ['docker', 'run', '-d', '--name', name + '-pg', '--network', name,
            '--network-alias', 'schema-pg', '--label', 'orml.task=ORML-117',
            '--label', 'orml.actor=dev', '--tmpfs',
            '/var/lib/postgresql/data:uid=70,gid=70,mode=0700',
            '-e', 'POSTGRES_HOST_AUTH_METHOD=trust', '-e', 'POSTGRES_USER=postgres',
            'neemle/pg.lite:19']
    container = run(argv)
    record = {
        'at': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'networkId': network, 'containerId': container, 'argv': argv,
        'inspect': json.loads(run(['docker', 'inspect', container])),
    }
    (root / (name + '-fixture.json')).write_text(json.dumps(record, indent=2))
    print(json.dumps({'containerId': container, 'networkId': network}))


main()
