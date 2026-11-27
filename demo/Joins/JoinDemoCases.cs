using System.Data;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class JoinDemoCases
{
    private static JoinLimits Limits => new(4, 8, 128, 32, 32);

    internal static async Task RunAsync(OrmContext db)
    {
        await JoinDemoFixture.CreateAsync(db);
        await BasicAsync(db);
        await FiltersAsync(db);
        await SnapshotPageAsync(db);
        await ChainAsync(db);
        await SelfAsync(db);
        await BorrowedAsync(db);
        await RefusalsAsync(db);
        await CancellationAsync(db);
    }

    private static JoinQuery Left(JoinSource<GraphAuthor> a, JoinSource<GraphBook> b, JoinLimits? limits = null) =>
        Joined.From(a, limits ?? Limits).LeftJoin(b, a.Column(x => x.Id).Equal(b.Column(x => x.AuthorId)));

    private static async Task BasicAsync(OrmContext db)
    {
        var a = GraphAuthorOrm.Query(db).AsJoinSource("author");
        var b = GraphBookOrm.Query(db).AsJoinSource("book");
        var select = new JoinSelection();
        var name = select.AddRequired(a.Column(x => x.Name), "authorName");
        var title = select.Add(b.Column(x => x.Title), "bookTitle");
        var editor = select.Add(b.Column(x => x.EditorId), "editorId");
        using var counter = new JoinDemoCounter();
        var query = Left(a, b).OrderBy(a.Column(x => x.Name)).ThenBy(b.Column(x => x.Title));
        var rows = await query.ToListAsync(select);
        JoinDemoChecks.Require(rows.Select(r => r.Get(name).Value).SequenceEqual(["Aster", "Aster", "Birch", "Cedar"]));
        JoinDemoChecks.Require(rows.Take(3).Select(r => r.Get(title).Value).SequenceEqual(["Alpha", "Beta", "Gamma"]));
        Presence(rows, title, editor);
        JoinDemoChecks.Require(counter.Attempted == 1);
        JoinDemoChecks.Report("left-presence", rows.Count, counter.Attempted);
        var inner = Joined.From(a, Limits).InnerJoin(b, a.Column(x => x.Id).Equal(b.Column(x => x.AuthorId)));
        var matched = await inner.ToListAsync(select);
        JoinDemoChecks.Require(matched.Count == 3 && matched.Count(r => r.Get(name).Value == "Aster") == 2);
        JoinDemoChecks.Require(counter.Attempted == 2);
        JoinDemoChecks.Report("inner-multiplicity", matched.Count, 1);
    }

    private static void Presence(List<JoinedRow> rows, JoinSlot<string> title, JoinSlot<Guid?> editor)
    {
        var matchedNull = rows[0].Get(editor);
        var matchedValue = rows[1].Get(editor);
        var absent = rows[3].Get(editor);
        JoinDemoChecks.Require(matchedNull.SourcePresent && matchedNull.IsNull);
        JoinDemoChecks.Require(matchedValue.SourcePresent && !matchedValue.IsNull);
        JoinDemoChecks.Require(matchedValue.Value == JoinDemoFixture.Birch);
        JoinDemoChecks.Require(!absent.SourcePresent && absent.IsNull && !rows[3].Get(title).SourcePresent);
        JoinDemoChecks.Missing(matchedNull);
        JoinDemoChecks.Missing(absent);
    }

    private static async Task FiltersAsync(OrmContext db)
    {
        var a = GraphAuthorOrm.Query(db).AsJoinSource("author");
        var b = GraphBookOrm.Query(db).Where(x => x.Title == "Alpha").AsJoinSource("filtered");
        var select = new JoinSelection();
        var name = select.Add(a.Column(x => x.Name), "name");
        var title = select.Add(b.Column(x => x.Title), "title");
        using var counter = new JoinDemoCounter();
        var query = Left(a, b).OrderBy(a.Column(x => x.Name));
        var onRows = await query.ToListAsync(select);
        JoinDemoChecks.Require(onRows.Count == 3 && onRows[0].Get(title).Value == "Alpha");
        JoinDemoChecks.Require(!onRows[1].Get(title).SourcePresent && !onRows[2].Get(title).SourcePresent);
        var outer = await query.Where(b.Column(x => x.Title).EqualValue("Alpha")).ToListAsync(select);
        JoinDemoChecks.Require(outer.Count == 1 && outer[0].Get(name).Value == "Aster");
        JoinDemoChecks.Require(counter.Attempted == 2);
        JoinDemoChecks.Report("right-on-and-outer-where", outer.Count, counter.Attempted);
    }

    private static async Task SnapshotPageAsync(OrmContext db)
    {
        var original = GraphAuthorOrm.Query(db).Where(x => x.Name != "Cedar").OrderBy(x => x.Name).Skip(1).Take(2);
        var a = original.AsJoinSource("author");
        var b = GraphBookOrm.Query(db).Where(x => x.Title != "never").AsJoinSource("book");
        original.Where(x => x.Name == "mutated");
        var select = new JoinSelection();
        var name = select.Add(a.Column(x => x.Name), "name");
        var title = select.Add(b.Column(x => x.Title), "title");
        var query = Left(a, b).Where(b.Column(x => x.Title).NotEqualValue("bound'--"))
            .ThenBy(b.Column(x => x.Title));
        using var counter = new JoinDemoCounter();
        var pending = query.ToListAsync(select);
        var later = select.Add(a.Column(x => x.Id), "later");
        var rows = await pending;
        JoinDemoChecks.Require(rows.Select(r => r.Get(name).Value + "/" + r.Get(title).Value)
            .SequenceEqual(["Aster/Beta", "Birch/Gamma"]));
        await JoinDemoChecks.ErrorAsync(() => Task.FromResult(rows[0].Get(later)), JoinError.WrongSlot);
        JoinDemoChecks.Require(counter.Attempted == 1);
        JoinDemoChecks.Report("captured-parameters-and-joined-page", rows.Count, counter.Attempted);
    }

    private static async Task ChainAsync(OrmContext db)
    {
        var a = GraphAuthorOrm.Query(db).AsJoinSource("author");
        var b = GraphBookOrm.Query(db).AsJoinSource("firstBook");
        var c = GraphBookOrm.Query(db).AsJoinSource("secondBook");
        var d = GraphAuthorOrm.Query(db).AsJoinSource("echoAuthor");
        var query = Left(a, b).LeftJoin(c, b.Column(x => x.AuthorId).Equal(c.Column(x => x.AuthorId)))
            .InnerJoin(d, c.Column(x => x.AuthorId).Equal(d.Column(x => x.Id)))
            .OrderBy(a.Column(x => x.Name)).ThenBy(b.Column(x => x.Title)).ThenBy(c.Column(x => x.Title));
        var select = new JoinSelection();
        var name = select.AddRequired(a.Column(x => x.Name), "name");
        var first = select.Add(b.Column(x => x.Title), "first");
        var second = select.Add(c.Column(x => x.Title), "second");
        var echo = select.AddRequired(d.Column(x => x.Name), "echo");
        using var counter = new JoinDemoCounter();
        var rows = await query.ToListAsync(select);
        JoinDemoChecks.Require(rows.Count == 5 && rows.All(r => r.Get(name).Value == r.Get(echo).Value));
        JoinDemoChecks.Require(rows.Select(r => r.Get(first).Value + "/" + r.Get(second).Value)
            .SequenceEqual(["Alpha/Alpha", "Alpha/Beta", "Beta/Alpha", "Beta/Beta", "Gamma/Gamma"]));
        JoinDemoChecks.Require(counter.Attempted == 1);
        JoinDemoChecks.Report("four-occurrences-previous-edge-chain", rows.Count, counter.Attempted);
    }

    private static async Task SelfAsync(OrmContext db)
    {
        var a = GraphAuthorOrm.Query(db).AsJoinSource("author");
        var b = GraphAuthorOrm.Query(db).AsJoinSource("sameType");
        var query = Joined.From(a, Limits).InnerJoin(b, a.Column(x => x.Name).Equal(b.Column(x => x.Name)));
        var select = new JoinSelection();
        var left = select.Add(a.Column(x => x.Id), "leftId");
        var right = select.Add(b.Column(x => x.Id), "rightId");
        using var counter = new JoinDemoCounter();
        var rows = await query.ToListAsync(select);
        JoinDemoChecks.Require(rows.Count == 3 && rows.All(r => r.Get(left).Value == r.Get(right).Value));
        JoinDemoChecks.Require(counter.Attempted == 1);
        JoinDemoChecks.Report("self-join", rows.Count, counter.Attempted);
    }

    private static async Task BorrowedAsync(OrmContext db)
    {
        await using var connection = await db.DataSource.OpenConnectionAsync();
        Console.WriteLine("joins native-server-version=" + connection.ServerVersion);
        await using var transaction = await connection.BeginTransactionAsync();
        var a = GraphAuthorOrm.Query(db).On(connection, transaction).AsJoinSource("boundRoot");
        var b = GraphBookOrm.Query(db).AsJoinSource("inheritedRight");
        var select = new JoinSelection();
        select.Add(a.Column(x => x.Name), "name");
        using (var counter = new JoinDemoCounter())
        {
            var rows = await Left(a, b).ToListAsync(select);
            JoinDemoChecks.Require(rows.Count == 4 && counter.Attempted == 1);
            JoinDemoChecks.Report("borrowed-root-unbound-right", rows.Count, counter.Attempted);
        }
        JoinDemoChecks.Require(connection.State == ConnectionState.Open);
        JoinDemoChecks.Require(ReferenceEquals(transaction.Connection, connection));
        var count = await GraphAuthorOrm.Query(db).On(connection, transaction).CountAsync();
        JoinDemoChecks.Require(count == 3);
        await transaction.RollbackAsync();
    }

    private static async Task RefusalsAsync(OrmContext db)
    {
        var a = GraphAuthorOrm.Query(db).AsJoinSource("author");
        var b = GraphBookOrm.Query(db).AsJoinSource("book");
        var query = Left(a, b, new(4, 8, 128, 32, 2)).OrderBy(a.Column(x => x.Name));
        var required = new JoinSelection();
        required.AddRequired(b.Column(x => x.Title), "requiredRight");
        var select = new JoinSelection();
        select.Add(a.Column(x => x.Name), "name");
        using var counter = new JoinDemoCounter();
        await JoinDemoChecks.ErrorAsync(() => query.ToListAsync(required), JoinError.RequiredNullable);
        await JoinDemoChecks.ErrorAsync(() => query.Take(3).ToListAsync(select), JoinError.LimitExceeded);
        JoinDemoChecks.Require(counter.Attempted == 0);
        var empty = await query.Take(0).ToListAsync(select);
        JoinDemoChecks.Require(empty.Count == 0 && counter.Attempted == 0);
        await JoinDemoChecks.ErrorAsync(() => query.ToListAsync(select), JoinError.LimitExceeded);
        JoinDemoChecks.Require(counter.Attempted == 1);
        JoinDemoChecks.Report("required-limit-overflow-and-take-zero", 0, counter.Attempted);
    }

    private static async Task CancellationAsync(OrmContext db)
    {
        var a = GraphAuthorOrm.Query(db).AsJoinSource("author");
        var b = GraphBookOrm.Query(db).AsJoinSource("book");
        var select = new JoinSelection();
        select.Add(a.Column(x => x.Name), "name");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var counter = new JoinDemoCounter();
        try { await Left(a, b).ToListAsync(select, canceled.Token); }
        catch (JoinReadCanceledException error)
        {
            JoinDemoChecks.Require(error.CancellationToken == canceled.Token && error.InnerException is not null);
            JoinDemoChecks.Require(counter.Attempted == 0);
            JoinDemoChecks.Report("pre-canceled", 0, counter.Attempted);
            return;
        }
        throw new InvalidOperationException("Expected caller cancellation.");
    }
}
