using System.Reflection;
using Neemle.OrmLite.Tests.Streaming.CiIntegration;
using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Neemle.OrmLite;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.Reads;

public sealed class SoftDeleteRootReadCensusTests(SoftDeleteDb db) : IClassFixture<SoftDeleteDb>
{
    private static readonly string[] WritePaths =
    [
        "RenderUpsert", "UpsertAsync", "RenderUpdateReturning", "UpdateReturningAsync",
        "RenderDeleteReturning", "DeleteReturningAsync", "RenderInsertMany", "InsertManyAsync",
        "RenderUpdate", "UpdateAsync", "RenderDelete", "DeleteAsync", "RenderPurge", "PurgeAsync",
        "RenderRestore", "RestoreAsync",
    ];

    private static readonly IReadOnlyList<RootReadPath> Paths =
        [.. RootReadPath.Census, .. StreamRootReadPaths.Census];

    public static TheoryData<string> PathNames => new(Paths.Select(p => p.Name));

    [Theory(DisplayName = "AC1: a soft root read path executes without the soft-deleted row")]
    [MemberData(nameof(PathNames))]
    public async Task A_root_read_path_excludes_the_soft_deleted_row(string path)
    {
        var read = Paths.Single(p => p.Name == path);

        Assert.Equal(read.Live, await read.Run(db.Roots()));
    }

    [Theory(DisplayName = "AC2: IncludingDeleted makes a root read path return the soft-deleted row too")]
    [MemberData(nameof(PathNames))]
    public async Task IncludingDeleted_returns_the_soft_deleted_row_on_a_root_read_path(string path)
    {
        var read = Paths.Single(p => p.Name == path);

        Assert.Equal(read.AllRows, await read.Run(db.Roots().IncludingDeleted()));
    }

    [Fact(DisplayName = "AC1: the census names every public read path of Query<T>, so a new one cannot be added unexecuted")]
    public void The_census_names_every_public_read_path()
    {
        var published = typeof(Query<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => n.EndsWith("Async", StringComparison.Ordinal) || n.StartsWith("Render", StringComparison.Ordinal))
            .Except(WritePaths)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var named = Paths.SelectMany(p => p.Members).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(published, named);
    }
}
