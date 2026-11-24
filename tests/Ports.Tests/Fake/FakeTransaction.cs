using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Ports.Tests.Fake;

public sealed class FakeTransaction(FakeConnection connection, IsolationLevel level) : DbTransaction
{
    private bool _finished;

    public override IsolationLevel IsolationLevel => level;

    protected override DbConnection DbConnection => connection;

    public override void Commit() => Finish("commit");

    public override void Rollback() => Finish("rollback");

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_finished) Rollback();
        base.Dispose(disposing);
    }

    private void Finish(string outcome)
    {
        _finished = true;
        connection.Store.Note(outcome);
    }
}
