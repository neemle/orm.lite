import datetime
import hashlib
import json
import pathlib
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

P = pathlib.Path('artifacts/graph/demo-current')
W = pathlib.Path.cwd()
COMMIT = '2ad9d96659bfdf6140f6fec9277ce75bda3206fd'
VERSION = '0.1.0-dev.g2ad9d96'
now = lambda: datetime.datetime.now(datetime.timezone.utc).isoformat()
sha = lambda data: hashlib.sha256(data).hexdigest()
git = lambda *a: subprocess.check_output(['git', *a], text=True).strip()
write = lambda name, value: (P / name).write_text(json.dumps(value, indent=2) + '\n')
records = {p.stem: json.loads(p.read_text()) for p in P.glob('*.json') if 'gate' in json.loads(p.read_text())}
assert len(records) == 15, records.keys()
assert git('rev-parse', 'HEAD') == COMMIT
for name, record in records.items():
    assert record['exitCode'] == 0 and record['commit'] == COMMIT, name
    assert not record['sourceChangedDuringRun'], name
    for filename, digest in record['sourceHashes'].items():
        assert sha(pathlib.Path(filename).read_bytes()) == digest, (name, filename)

legs = []
for major in ['10', '11']:
    for mode in ['jit', 'native']:
        for provider in ['sqlite', 'postgres']:
            name = mode + major + '-' + provider
            raw = (P / (name + '.log')).read_bytes()
            header, separator, result = raw.partition(b'\n')
            assert separator and header.startswith(b'graph chapter runtime=')
            assert b'False' not in result and result.endswith(b'graph chapter ok; scratch removed\n')
            record = records[name]
            legs.append({'name': name, 'runtime': header.decode().split('=', 1)[1],
                         'provider': provider, 'mode': mode, 'exitCode': record['exitCode'],
                         'started': record['started'], 'finished': record['finished'],
                         'result': result.decode(), 'resultSha256': sha(result)})
assert len({leg['result'] for leg in legs}) == 1

original = ['GraphChapter.cs', 'GraphAuthor.cs', 'GraphBook.cs', 'GraphTag.cs', 'GraphFixture.cs']
original_paths = ['demo/Graph/' + name for name in original]
assert not git('diff', '--name-only', 'e32b9fc1816ccee91fa20acc2aac1eef71751d3d', 'HEAD', '--', *original_paths)
packages = []
for path in sorted((P / 'feed').glob('*.nupkg')):
    with zipfile.ZipFile(path) as archive:
        spec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
        package_id = next(e.text for e in spec.iter() if e.tag.split('}')[-1] == 'id')
        version = next(e.text for e in spec.iter() if e.tag.split('}')[-1] == 'version')
        assert version == VERSION
        relative = package_id.lower() + '/' + VERSION
        entries = []
        for name in archive.namelist():
            if not (name.endswith(('.dll', '.props', '.targets')) or '/ormlite.capabilities.' in name):
                continue
            data = archive.read(name)
            cache = P / 'packages' / relative / name
            assert cache.read_bytes() == data, str(cache)
            entries.append({'entry': name, 'sha256': sha(data), 'consumedCacheByteEqual': True})
        packages.append({'id': package_id, 'version': version, 'file': str(path.relative_to(P)),
                         'sha256': sha(path.read_bytes()), 'entries': entries})
assert len(packages) == 3

assets = []
for build in ['jit10-build', 'jit11-build', 'native10-build', 'native11-build']:
    path = P / build / 'obj/OrmLiteDemo/project.assets.json'
    parsed = json.loads(path.read_text())
    actual = sorted(k for k in parsed['libraries'] if k.startswith('Neemle.OrmLite'))
    expected = sorted(package['id'] + '/' + VERSION for package in packages)
    assert actual == expected, (build, actual)
    assert list(parsed['packageFolders']) == [str((P / 'packages').resolve())]
    props = path.parent / 'OrmLiteDemo.csproj.nuget.g.props'
    targets = path.parent / 'OrmLiteDemo.csproj.nuget.g.targets'
    assert VERSION in props.read_text() and VERSION in targets.read_text()
    assets.append({'file': str(path.relative_to(P)), 'sha256': sha(path.read_bytes()),
                   'privatePackages': actual, 'imports': [str(props.relative_to(P)), str(targets.relative_to(P))]})

for build in ['jit10-build', 'jit11-build']:
    output = P / build / 'bin/OrmLiteDemo/release'
    for package in packages:
        for entry in package['entries']:
            if entry['entry'].startswith('lib/') and entry['entry'].endswith('.dll'):
                assert sha((output / pathlib.Path(entry['entry']).name).read_bytes()) == entry['sha256']

generated = []
for major in ['10', '11']:
    for entity in ['GraphAuthor', 'GraphBook', 'GraphTag']:
        files = [path for path in (P / ('generated' + major)).rglob('*.cs')
                 if re.search(r'\bclass\s+' + entity + r'Orm\b', path.read_text())]
        assert len(files) == 1, (major, entity)
        path = files[0]
        assert 'GraphModel.Register' in path.read_text() and 'class Paths' in path.read_text()
        generated.append({'entity': entity, 'sdk': major, 'file': str(path.relative_to(P)),
                          'sha256': sha(path.read_bytes())})

binaries = [{'file': 'native' + major + '-out/OrmLiteDemo',
             'sha256': sha((P / ('native' + major + '-out/OrmLiteDemo')).read_bytes()),
             'bytes': (P / ('native' + major + '-out/OrmLiteDemo')).stat().st_size} for major in ['10', '11']]
write('parity-provenance.json', {'generated': now(), 'executionCommit': COMMIT,
      'sourceTree': git('rev-parse', 'HEAD:src'), 'testTree': git('rev-parse', 'HEAD:tests'),
      'legs': legs, 'normalization': 'Remove ONLY first graph chapter runtime= line, retain every remaining byte.',
      'allEightEquivalent': True, 'originalChapterPathsUnchangedFrom': 'e32b9fc1816ccee91fa20acc2aac1eef71751d3d',
      'originalChapterPaths': original_paths, 'sourceInputStability': 'All15 sourceChangedDuringRun=false and current source bytes match recorder hashes.',
      'packages': packages, 'assets': assets, 'jitOutputLibraryBytesEqualPackages': True,
      'generatedGraphSources': generated, 'binaries': binaries})
print('PARITY_PROVENANCE_COMPLETE', len(legs), len(packages), len(assets), len(generated), flush=True)
