using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Neemle.OrmLite.Tests.SoftDelete.Reads;

namespace Neemle.OrmLite.Tests.Streaming.CiIntegration;

internal static class StreamRootReadPaths
{
    internal static readonly IReadOnlyList<RootReadPath> Census =
    [
        new("StreamAsync", [nameof(Query<SoftRoot>.StreamAsync)],
            async q => (await StreamingProviderChecks.CollectAsync(q.OrderBy(r => r.Rank).StreamAsync()))
                .Select(r => (long)r.Rank).ToArray(), Live, All),
        new("StreamProjectAsync", [nameof(Query<SoftRoot>.StreamProjectAsync)],
            q => RanksAsync(q.OrderBy(r => r.Rank).StreamProjectAsync([r => r.Rank], r => r.GetInt32(0))),
            Live, All),
        new("StreamProjectionAsync", [nameof(Query<SoftRoot>.StreamProjectionAsync)],
            q => RanksAsync(q.OrderBy(r => r.Rank).ProjectColumn(r => r.Rank)
                .StreamProjectionAsync(r => r.GetInt32(0))), Live, All),
        new("StreamColumnAsync", [nameof(Query<SoftRoot>.StreamColumnAsync)],
            q => RanksAsync(q.OrderBy(r => r.Rank).StreamColumnAsync<int>(r => r.Rank)), Live, All),
    ];

    private static long[] Live => [SoftDeleteDb.LiveRank];
    private static long[] All => [SoftDeleteDb.LiveRank, SoftDeleteDb.DeletedRank];

    private static async Task<long[]> RanksAsync(QueryStream<int> stream) =>
        (await StreamingProviderChecks.CollectAsync(stream)).Select(r => (long)r).ToArray();
}
