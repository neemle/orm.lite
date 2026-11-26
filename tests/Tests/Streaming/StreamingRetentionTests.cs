using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class StreamingRetentionTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<StreamRow> Observe(QueryStreamEnumerator<StreamRow> e) => new(e.Current);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Alive(WeakReference<StreamRow> reference) => reference.TryGetTarget(out _);

    [Fact(DisplayName = "[UC-010] an advanced enumerator retains Current without retaining prior mapped rows")]
    public async Task Previous_row_is_collectible_while_the_cursor_and_current_row_remain_live()
    {
        var probe = new StreamProbe { Count = 1000000 };
        await using var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var previous = Observe(e);
        Assert.True(await e.MoveNextAsync());
        for (var i = 0; i < 4 && Alive(previous); i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        Assert.False(Alive(previous));
        Assert.Equal(2, e.Current.Id);
        Assert.Equal(2, probe.Reads);
        Assert.Equal(0, probe.ReaderDisposals);
    }
}
