import pathlib,hashlib,json,subprocess,difflib,datetime
root=pathlib.Path.cwd();out=root/'artifacts/streaming/sdet/control-proposal';out.mkdir(parents=True,exist_ok=True)
proposed={}
def read(p):return (root/p).read_text()
p='tests/Tests/SoftDelete/Reads/RootReadPath.cs';s=read(p)
entries='''        new("StreamAsync", [nameof(Query<SoftRoot>.StreamAsync)],
            q => ConsumeStream(q.OrderBy(r => r.Rank).StreamAsync(), r => r.Rank),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),
        new("StreamProjectAsync", [nameof(Query<SoftRoot>.StreamProjectAsync)],
            q => ConsumeStream(q.OrderBy(r => r.Rank).StreamProjectAsync([r => r.Rank], r => r.GetInt32(0)), r => r),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),
        new("StreamProjectionAsync", [nameof(Query<SoftRoot>.StreamProjectionAsync)],
            q => ConsumeStream(q.OrderBy(r => r.Rank).ProjectColumn(r => r.Rank)
                .StreamProjectionAsync(r => r.GetInt32(0)), r => r),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),
        new("StreamColumnAsync", [nameof(Query<SoftRoot>.StreamColumnAsync)],
            q => ConsumeStream(q.OrderBy(r => r.Rank).StreamColumnAsync<int>(r => r.Rank), r => r),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),
'''
s=s.replace('    [\n        new("ToListAsync"','    [\n'+entries+'        new("ToListAsync"',1)
helper='''    private static async Task<long[]> ConsumeStream<T>(QueryStream<T> stream, Func<T, long> rank)
    {
        var result = new List<long>();
        await using var e = stream.GetAsyncEnumerator();
        while (await e.MoveNextAsync()) result.Add(rank(e.Current));
        var completion = await e.Completion;
        if (completion.Reason != QueryStreamStopReason.Completed || !completion.CleanupSucceeded)
            throw new InvalidOperationException("stream census did not establish clean EOF");
        return result.ToArray();
    }

'''
s=s.replace('    public static RootReadPath Named',helper+'    public static RootReadPath Named');proposed[p]=s
p='tests/Tests/QueryIdentifierSurfaceTests.cs';proposed[p]=read(p).replace('                "Boolean",','                "Boolean",\n                "CancellationToken",',1)
p='tests/Tests/QueryTerminalState/TerminalCensus.cs';proposed[p]=read('artifacts/streaming/census-proposal/TerminalCensus.cs')
reasons={
'Query.StreamAsync/1':'cold row-stream capture shares CompileSelect text; SdetSoftReadCensusTests executes visibility and SdetProviderTests executes parity, no-write and Completion on both providers',
'Query.StreamProjectAsync/3':'cold projection-stream capture shares CompileProject text; SdetProviderTests executes frozen columns, no-write and Completion on both providers',
'Query.StreamProjectionAsync/2':'cold configured-projection capture shares CompileProjection text; SdetProviderTests executes no-write and Completion on both providers',
'Query.StreamColumnAsync/2':'row-wise scalar stream shares CompileProject text, not ScalarAsync first-row cap; SdetProviderTests executes typed NULL/default, no-write and Completion on both providers',
'Query.get_StreamMap/0':'internal captured trusted mapper identity; SdetPrimaryCleanupTests and SdetProviderTests exercise exact cause and detached-row mapping, emits no SQL',
'Query.get_StreamBinding/0':'internal provider/data-source snapshot; producer gated-open and independent real-provider diagnostic/lock witnesses execute this path, emits no SQL',
'Query.get_StreamBorrowed/0':'internal borrowed connection/transaction snapshot; SdetBorrowedBoundaryTests executes exact-identity isolation and mismatch refusal, emits no SQL',
'Query.get_StreamLocked/0':'internal row-lock metadata snapshot; SdetBorrowedBoundaryTests and SdetProviderTests execute transaction prerequisite and actual PostgreSQL lock path, emits no SQL'}
p='tests/Tests/Corpus/Support/CorpusSurface.cs';s=read(p);marker='        ["Query.BuildSelect/0"]';at=s.index(marker);s=s[:at]+''.join('        ["'+k+'"] = "'+v+'",\n' for k,v in reasons.items())+s[at:];proposed[p]=s
p='tests/Tests/Corpus/INDEX.md';s=read(p).replace('| `Query` | 108 | 95 | 13 |','| `Query` | 116 | 95 | 21 |').replace('| total | 234 | 200 | 34 |','| total | 242 | 200 | 42 |')
marker='### Excluded members and why\n\n| member | reason |\n| --- | --- |\n';at=s.index(marker)+len(marker);end=s.index('\n\n',at);rows=s[at:end].splitlines()+['| `'+k+'` | '+v+' |' for k,v in reasons.items()];rows.sort(key=lambda x:x.split('`')[1]);s=s[:at]+'\n'.join(rows)+s[end:];proposed[p]=s
items=[];patch=''
for path,text in proposed.items():
 base=(root/path).read_bytes();dest=out/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(text)
 items.append({'path':path,'baseSha256':hashlib.sha256(base).hexdigest(),'proposedSha256':hashlib.sha256(dest.read_bytes()).hexdigest()})
 patch+=''.join(difflib.unified_diff(base.decode().splitlines(True),text.splitlines(True),fromfile='a/'+path,tofile='b/'+path))
(out/'control-proposal.patch').write_text(patch)
manifest={'status':'UNAPPLIED / UNAPPROVED; proposal only; existing controls untouched','generated':datetime.datetime.now(datetime.timezone.utc).isoformat(),'currentHead':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'sourceTree':subprocess.check_output(['git','rev-parse','HEAD:src'],text=True).strip(),'files':items,'fiveObligations':['SoftDeleteRootReadCensus: execute all four streams, live and IncludingDeleted','QueryIdentifierSurface: typed CancellationToken, no string allowance widened','TerminalCensus: consume all four to clean EOF, existing assertions preserved','CorpusSurface: four public captures and four internal getters with explicit executed rationale','CorpusINDEX: derived Query116/95/21 and total242/200/42 plus eight exclusions'],'limits':['No existing source/control/golden applied or regenerated','Proposal not compiled or approved; application needs rightful writer claim','Old stream row-lock census remains its original four terminals; streaming lock prerequisite independently executed','Corpus SQL goldens unchanged; exclusions classify shared compiler and non-SQL lifecycle, not behavioral exemption'],'executedWitnesses':['SdetSoftReadCensusTests eight real SQLite cases','SdetProviderTests Stream_terminals_do_not_write_the_query eight actual-provider cases','Independent scoped provider fixtures and retained typed Completion']}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps({'files':len(items),'patchSha256':hashlib.sha256(patch.encode()).hexdigest()}))
