using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public sealed class FinalLockTests
{
    public static IEnumerable<object[]> LockCases()
    {
        yield return [Support.Native, Support.Native, false, true];
        yield return [Support.Native, Support.Emulated, false, true];
        yield return [Support.Native, Support.None, false, true];
        yield return [Support.Emulated, Support.Native, false, false];
        yield return [Support.Emulated, Support.Emulated, false, false];
        yield return [Support.Emulated, Support.None, false, false];
        yield return [Support.None, Support.Native, false, false];
        yield return [Support.None, Support.Emulated, false, false];
        yield return [Support.None, Support.None, false, false];
        yield return [Support.Native, Support.Native, true, true];
        yield return [Support.Native, Support.Emulated, true, false];
        yield return [Support.Native, Support.None, true, false];
        yield return [Support.Emulated, Support.Native, true, false];
        yield return [Support.Emulated, Support.Emulated, true, false];
        yield return [Support.Emulated, Support.None, true, false];
        yield return [Support.None, Support.Native, true, false];
        yield return [Support.None, Support.Emulated, true, false];
        yield return [Support.None, Support.None, true, false];
    }

    [Theory(DisplayName = "[UC-010][AC09] only native capabilities required by the requested lock admit streaming")]
    [MemberData(nameof(LockCases))]
    public async Task Required_native_lock_matrix(Support row, Support preserving, bool key, bool accepted)
    {
        var probe = new StreamProbe();
        var caps = CapabilitySet.Everything().With(Capability.RowLock, row)
            .With(Capability.KeyPreservingRowLock, preserving).With(Capability.PartialIndex, Support.Emulated);
        var query = FinalQueries.Query(probe, new FinalCapabilityProvider(caps));
        await using var connection = new StreamProbeConnection(probe);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        query.On(connection, transaction);
        if (!accepted)
        {
            Refused(probe, () => Locked(query, key).StreamAsync());
            return;
        }
        var e = Locked(query, key).StreamAsync().GetAsyncEnumerator();
        Assert.Equal((0, 0), (probe.Commands, probe.Executions));
        Assert.True(await e.MoveNextAsync());
        Assert.Same(transaction, probe.Transaction);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Equal((1, 0), (probe.Executions, probe.ConnectionDisposals));
    }

    [Theory(DisplayName = "[UC-010][AC09] emulated lock still requires a transaction before capture refusal")]
    [InlineData(false)] [InlineData(true)]
    public async Task Transaction_prerequisite_remains_cold(bool key)
    {
        var p = new StreamProbe();
        var provider = new FinalCapabilityProvider(CapabilitySet.Everything(Support.Emulated));
        var query = FinalQueries.Query(p, provider);
        var error = Assert.Throws<QueryStreamException>(() => Locked(query, key).StreamAsync());
        Assert.Equal(QueryStreamError.ResourceMismatch, error.Error);
        Assert.Equal((0, 0, 0), (p.Connections, p.Commands, p.Executions));
    }

    private static Query<StreamRow> Locked(Query<StreamRow> query, bool key) =>
        key ? query.ForNoKeyUpdate() : query.ForUpdate();

    private static void Refused(StreamProbe probe, Action capture)
    {
        var failure = Record.Exception(capture);
        Assert.NotNull(failure);
        if (failure is QueryStreamException stream)
        {
            Assert.Equal(QueryStreamError.InvalidRequest, stream.Error);
            Assert.IsType<CapabilityNotSupportedException>(stream.InnerException);
            Assert.DoesNotContain("capability-probe", stream.ToString());
        }
        else Assert.IsType<CapabilityNotSupportedException>(failure);
        Assert.Equal((0, 0), (probe.Commands, probe.Executions));
    }
}
