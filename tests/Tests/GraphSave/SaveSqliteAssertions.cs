using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveSqliteAssertions
{
    internal static void OneTransaction(SaveSqliteDatabase db, int statements, TransactionPhase ending)
    {
        Assert.Equal(statements, db.Statements.Count);
        var transaction = Assert.Single(db.Statements.Select(s => s.TransactionId).Distinct());
        Assert.NotNull(transaction);
        Assert.True(transaction > 0);
        Assert.Single(db.Statements.Select(s => s.ConnectionId).Distinct());
        Assert.Equal(new[] { TransactionPhase.Begin, ending }, db.Transactions.Select(t => t.Phase));
        Assert.All(db.Transactions, t => Assert.Equal(transaction, t.TransactionId));
        Assert.Equal(statements, db.Transactions.Last().StatementCount);
        Assert.All(db.Statements, s => { Assert.Equal("graph-save", s.Text); Assert.Null(s.Parameters); });
    }

    internal static GraphSaveRequest<SaveProbeNode> Request(SaveProbeNode root, SaveProbeNode child, SaveProbeTag tag,
        bool repeat = false) => new(root, repeat ? GraphSaveIntent.Update : GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty.With(child, repeat ? GraphSaveIntent.Update : GraphSaveIntent.Insert)
                .With(tag, repeat ? GraphSaveIntent.Unchanged : GraphSaveIntent.Insert),
                GraphSavePermissions.None.Allow(SaveProbeModel.Children,
                    new(GraphSaveActions.Insert | GraphSaveActions.Update, GraphSaveKeyBinding.BindInWriteBuffer))
                    .Allow(SaveProbeModel.TagEdge, new(GraphSaveActions.Insert | GraphSaveActions.AddLink)),
                new(new(2, 3, 2), new(3, 1, 4))));
}
