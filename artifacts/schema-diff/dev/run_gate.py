import datetime
import hashlib
import json
import pathlib
import subprocess
import sys


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def source_digest():
    digest = hashlib.sha256()
    names = subprocess.check_output(['git', 'ls-files', '-co', '--exclude-standard', '-z', '--', 'src'])
    for raw in sorted(names.split(b'\0')):
        if raw:
            path = pathlib.Path(raw.decode())
            if path.is_file():
                digest.update(raw + b'\0' + path.read_bytes() + b'\0')
    return digest.hexdigest()


def proof_digest():
    digest = hashlib.sha256()
    for path in sorted(pathlib.Path('artifacts/schema-diff/dev/foundation-proof').glob('*')):
        if path.is_file():
            digest.update(str(path).encode() + b'\0' + path.read_bytes() + b'\0')
    return digest.hexdigest()


def metadata(name, argv):
    return {
        'gate': name,
        'argv': argv,
        'cwd': str(pathlib.Path.cwd()),
        'commit': git('rev-parse', 'HEAD'),
        'sourceTree': git('rev-parse', 'HEAD:src'),
        'testTree': git('rev-parse', 'HEAD:tests'),
        'sourceDigest': source_digest(),
        'proofDigest': proof_digest(),
        'nativeInputs': native_inputs(),
        'faultInputs': fault_inputs(),
        'worktreeStatus': git('status', '--short'),
        'started': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    }


def native_inputs():
    root = pathlib.Path('artifacts/schema-diff/dev/bridge-generated')
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in sorted(root.glob('*'))
            if path.is_file() and path.suffix in ('.cs', '.csproj', '.json')}


def fault_inputs():
    root = pathlib.Path('artifacts/schema-diff/dev/fault-proof')
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in sorted(root.glob('*'))
            if path.is_file() and path.suffix in ('.cs', '.csproj')}


def main():
    name, argv = sys.argv[1], sys.argv[2:]
    path = pathlib.Path('artifacts/schema-diff/dev') / name
    result = metadata(name, argv)
    path.with_suffix('.json').write_text(json.dumps(result, indent=2))
    with path.with_suffix('.log').open('w') as output:
        result['exitCode'] = subprocess.call(argv, stdout=output, stderr=subprocess.STDOUT)
    result['sourceChangedDuringRun'] = source_digest() != result['sourceDigest']
    result['proofChangedDuringRun'] = proof_digest() != result['proofDigest']
    result['nativeInputsChangedDuringRun'] = native_inputs() != result['nativeInputs']
    result['faultInputsChangedDuringRun'] = fault_inputs() != result['faultInputs']
    result['finished'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    path.with_suffix('.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(result))
    return result['exitCode']


sys.exit(main())
