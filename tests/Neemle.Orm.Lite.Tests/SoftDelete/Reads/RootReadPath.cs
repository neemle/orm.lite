using Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;
using Neemle.Orm.Lite;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Reads;

public sealed record RootReadPath(
    string Name,
    string[] Members,
    Func<Query<SoftRoot>, Task<long[]>> Run,
    long[] Live,
    long[] AllRows)
{
    public static readonly IReadOnlyList<RootReadPath> Census =
    [
        new("ToListAsync", [nameof(Query<SoftRoot>.ToListAsync), nameof(Query<SoftRoot>.RenderSelect)],
            async q => (await q.OrderBy(r => r.Rank).ToListAsync()).Select(r => (long)r.Rank).ToArray(),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),

        new("FirstOrDefaultAsync", [nameof(Query<SoftRoot>.FirstOrDefaultAsync)],
            async q => [(await q.OrderByDescending(r => r.Rank).FirstOrDefaultAsync())!.Rank],
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.DeletedRank]),

        new("CountAsync", [nameof(Query<SoftRoot>.CountAsync)],
            async q => [await q.CountAsync()],
            [1], [2]),

        new("SumAsync", [nameof(Query<SoftRoot>.SumAsync)],
            async q => [await q.SumAsync(r => r.Rank)],
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank + SoftDeleteDb.DeletedRank]),

        new("ScalarAsync", [nameof(Query<SoftRoot>.ScalarAsync)],
            async q =>
            {
                var rank = await q.Where(r => r.Name == SoftDeleteDb.DeletedName).ScalarAsync<object?>(r => r.Rank);
                return [rank is null ? -1 : Convert.ToInt64(rank)];
            },
            [-1], [SoftDeleteDb.DeletedRank]),

        new("AnyAsync", [nameof(Query<SoftRoot>.AnyAsync)],
            async q => [await q.Where(r => r.Name == SoftDeleteDb.DeletedName).AnyAsync() ? 1 : 0],
            [0], [1]),

        new("AggregatesAsync", [nameof(Query<SoftRoot>.AggregatesAsync), nameof(Query<SoftRoot>.RenderAggregates)],
            async q => (await q.AggregateCount().AggregateSum(r => r.Rank).AggregatesAsync()).Select(Convert.ToInt64).ToArray(),
            [1, SoftDeleteDb.LiveRank], [2, SoftDeleteDb.LiveRank + SoftDeleteDb.DeletedRank]),

        new("ProjectAsync", [nameof(Query<SoftRoot>.ProjectAsync), nameof(Query<SoftRoot>.RenderProject)],
            async q => (await q.OrderBy(r => r.Rank).ProjectAsync([r => r.Rank], reader => Convert.ToInt64(reader.GetValue(0)))).ToArray(),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),

        new("ProjectionAsync", [nameof(Query<SoftRoot>.ProjectionAsync), nameof(Query<SoftRoot>.RenderProjection)],
            async q => (await q.ProjectColumn(r => r.Rank).OrderBy(r => r.Rank).ProjectionAsync(reader => Convert.ToInt64(reader.GetValue(0)))).ToArray(),
            [SoftDeleteDb.LiveRank], [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank]),
    ];

    public static RootReadPath Named(string name) => Census.Single(p => p.Name == name);
}
