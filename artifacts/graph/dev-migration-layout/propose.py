from pathlib import Path
import hashlib,json,difflib,collections
out=Path('artifacts/graph/dev-migration-layout'); src=out/'after-capture/proposed'; proposed=out/'proposal/proposed'; files={}
def add(path,data):files[str(path)]=data
for f in src.rglob('*'):
 if f.is_file():add(f.relative_to(src),f.read_bytes())
surface=Path('tests/Tests/Corpus/Support/CorpusSurface.cs');s=surface.read_text();needle='    public static readonly IReadOnlyDictionary<string, string> Exclusions = new Dictionary<string, string>(StringComparer.Ordinal)\n    {\n';s=s.replace(needle,needle+'        ["Query.GraphSnapshot/0"] = "immutable query/policy/On-binding snapshot consumed by the graph planner; "\n            + "it emits no statement and is exercised by GraphReadTests, GraphBatchTests and GraphFailureTests",\n');add(surface,s.encode())
quality=Path('tests/Tests/GeneratorQualityTests.cs');s=quality.read_text();old='''        return result.Results.SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName == "OrmLite.Entities.g.cs").SourceText.ToString();''';new='''        var partitions = result.Results.SelectMany(r => r.GeneratedSources)
            .Where(s => s.HintName.StartsWith("OrmLite.Entities.", StringComparison.Ordinal))
            .OrderBy(s => s.HintName, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(partitions);
        return string.Join("\\n", partitions.Select(s => s.SourceText.ToString()));''';assert old in s;s=s.replace(old,new);add(quality,s.encode())
hygiene=Path('tests/Tests/GeneratedTextHygieneTests.cs');s=hygiene.read_text();s=s.replace('var (diagnostics, generated, _) = Run(source);','var (diagnostics, generated, output) = Run(source);');s=s.replace('''        Assert.Equal(3, lines.Count(l => l == Header));''','''        var files = output.SyntaxTrees.Where(tree => !string.IsNullOrEmpty(tree.FilePath))
            .OrderBy(tree => Path.GetFileName(tree.FilePath), StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "OrmLite.Entities.0000.g.cs", "OrmLite.Entities.0001.g.cs",
            "OrmLite.Entities.0002.g.cs", "OrmLite.Entities.0003.g.cs",
            "OrmLite.MigrationRegistry.g.cs", "OrmLite.Migrations.g.cs" },
            files.Select(tree => Path.GetFileName(tree.FilePath)));
        Assert.All(files, tree => Assert.Single(tree.ToString().Split('\\n')
            .Select(line => line.Trim()).Where(line => line == Header)));''');add(hygiene,s.encode())
# Stale pinned files are explicitly removed only in the unapplied proposal.
for base in ['tests/Tests/Corpus/Goldens','tests/Tests/OrmLiteGolden/Expected']:
 for f in Path(base).rglob('*.golden'):
  if str(f) not in files:files[str(f)]=None
rows=[];patch=[]
H=lambda b:hashlib.sha256(b).hexdigest() if b is not None else None
for path,new in sorted(files.items()):
 p=Path(path);old=p.read_bytes() if p.exists() else None
 if old==new:continue
 kind='REMOVE' if new is None else 'ADD' if old is None else 'MODIFY'
 target=proposed/path
 if new is not None:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(new)
 rows.append(dict(path=path,kind=kind,beforeSha256=H(old),proposedSha256=H(new),proposalFile=str(target) if new is not None else None))
 patch.extend(difflib.unified_diff((old or b'').decode().splitlines(True),(new or b'').decode().splitlines(True),fromfile='a/'+path,tofile='b/'+path,n=0))
(out/'proposal/changes.patch').write_text(''.join(patch))
(out/'proposal-manifest.json').write_text(json.dumps(dict(applied=False,approved=False,historical95='artifacts/graph/dev-generated-repair/init-proposal-manifest.json (immutable historical; this current capture supersedes no approvals)',counts=dict(collections.Counter(row['kind'] for row in rows)),files=rows),indent=2))
print(collections.Counter(row['kind'] for row in rows))
