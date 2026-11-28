using System.Data;
using System.Data.Common;
using Neemle.OrmLite.Tests.Streaming;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrphanGuardSessionFixture
{
    internal StreamProbe Probe { get; } = new();
    internal StreamProbeConnection Connection { get; }
    internal OrphanGuardProbeTransaction Transaction { get; }

    internal OrphanGuardSessionFixture()
    {
        Connection = new(Probe);
        Connection.Open();
        Transaction = new(Connection);
    }

    internal OrphanGuardSession Session(int budget = 10, DbTransaction? transaction = null)
    {
        var request = OrdinaryOrphanProfiles.Request(false);
        var limits = new OrphanLimits(10, 10, 10, 10, budget);
        return new(Connection, transaction ?? Transaction, new(request.Parent, request.Child,
            request.OwnerForeignKey, request.ParentKey, limits));
    }

    internal Task<IReadOnlyList<int>> Read(OrphanGuardSession session, CancellationToken ct = default,
        Func<DbDataReader, int>? map = null) => session.ReadAsync<int>(
            new("SELECT @p0", [new(0, "p0", 7)], StatementKind.Read), map ?? Map,
            10, OrphanError.LimitExceeded, ct);

    internal void AssertCallerOwned()
    {
        Assert.Equal(ConnectionState.Open, Connection.State);
        Assert.Equal(0, Probe.Closes);
        Assert.Equal(0, Probe.ConnectionDisposals);
        Assert.Equal(0, Transaction.Commits + Transaction.Rollbacks + Transaction.Disposals);
    }

    internal void AssertCleanup(int readers = 1)
    {
        Assert.Equal(readers, Probe.ReaderDisposals);
        Assert.Equal(1, Probe.CommandDisposals);
        AssertCallerOwned();
    }

    private int Map(DbDataReader reader) { Probe.Maps++; return reader.GetInt32(0); }
}
