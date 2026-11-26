using System.Text.Json;
using Xunit;
using static Neemle.OrmLite.Tests.Graph.Security.SecurityModel;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal static class SecurityAssertions
{
    internal static void Membership(IReadOnlyList<SecurityRoot> roots)
    {
        Assert.Equal(2, roots.Count);
        var a = Assert.Single(roots, r => r.Id == KeyA);
        var b = Assert.Single(roots, r => r.Id == KeyB);
        Assert.Equal(RowA, a.Payload);
        Assert.Equal(RowB, b.Payload);
        Assert.Equal(LeafA, Assert.Single(a.Children).Id);
        Assert.Equal(LeafB, Assert.Single(b.Children).Id);
        Assert.Same(a, a.Children[0].Parent);
        Assert.Same(b, b.Children[0].Parent);
        Assert.Same(b.Children[0], Assert.Single(a.Peers));
        Assert.Same(a.Children[0], Assert.Single(b.Peers));
        Assert.All(roots.SelectMany(r => r.Children), child => Assert.Equal(LeafRow, child.Payload));
    }

    internal static void BoundValues(SecurityDatabase db)
    {
        Assert.Equal(5, db.Source.Reads.Count);
        Assert.All(db.Source.Reads, command =>
        {
            Assert.DoesNotContain(KeyA, command.Text);
            Assert.DoesNotContain(KeyB, command.Text);
            Assert.DoesNotContain(LeafA, command.Text);
            Assert.DoesNotContain(LeafB, command.Text);
        });
        var values = db.Source.Reads.SelectMany(command => command.Parameters).ToArray();
        Assert.Contains(KeyA, values);
        Assert.Contains(KeyB, values);
        Assert.Contains(LeafA, values);
        Assert.Contains(LeafB, values);
        Assert.Contains(db.Source.Reads, command => command.Text.Contains(Quote(RootTable), StringComparison.Ordinal));
        Assert.Contains(db.Source.Reads, command => command.Text.Contains(Quote(LeafTable), StringComparison.Ordinal));
        Assert.Contains(db.Source.Reads, command => command.Text.Contains(Quote(LinkTable), StringComparison.Ordinal));
    }

    internal static void Privacy(SecurityDatabase db, bool logParameters)
    {
        Assert.NotEmpty(db.Sink.Statements);
        var text = JsonSerializer.Serialize(db.Sink.Statements.Select(e => new
        {
            e.Text, e.Parameters, e.Kind, e.Provider, Error = e.Error?.ToString(),
        }));
        foreach (var marker in new[] { KeyA, KeyB, LeafA, LeafB, RowA, RowB, LeafRow, Credential })
            Assert.DoesNotContain(JsonSerializer.Serialize(marker)[1..^1], text);
        Assert.All(db.Sink.Statements, e => Assert.Equal(StatementKind.Read, e.Kind));
        if (logParameters)
            Assert.Contains(db.Sink.Statements.SelectMany(e => e.Parameters!), p => p.Value == "[redacted]");
        else
            Assert.All(db.Sink.Statements, e => Assert.Null(e.Parameters));
        Assert.Empty(db.Sink.Transactions);
    }
}
