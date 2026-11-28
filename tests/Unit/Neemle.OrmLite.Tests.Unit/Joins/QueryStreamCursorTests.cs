using Xunit;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// The stream cursor is shared by joined reads and query streams; only a joined read hands it a plan check (ORML-151).
// A query stream, which has none, opens and reads every row as before.
public sealed class QueryStreamCursorTests
{
    [Fact(DisplayName = "a query stream, which has no plan check, opens and reads every row through the shared cursor")]
    public async Task A_query_stream_reads_every_row()
    {
        var db = new FakeJoinDatabase<QueryStreamCursorTests>();
        db.Probe.Headers = ["name"];
        db.Probe.Rows = [["Aster"], ["Birch"]];
        var names = new List<string>();

        await foreach (var name in db.Query().StreamColumnAsync<string>(x => x.Name)) names.Add(name);

        Assert.Equal(new[] { "Aster", "Birch" }, names);
        Assert.Equal(1, db.Probe.Commands);
    }
}
