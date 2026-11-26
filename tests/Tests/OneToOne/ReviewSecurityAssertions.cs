using System.Text.Json;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.OneToOne;

internal static class ReviewSecurityAssertions
{
    internal static void BoundReads(ReviewSecurityDatabase db)
    {
        Assert.Equal(8, db.Source.Reads.Count);
        Assert.All(db.Source.Reads, read => Assert.Single(read.Parameters));
        Assert.Equal(3, db.Source.Reads.Count(r => Equals(r.Parameters.Single(), ReviewPairSource.KeyA)));
        Assert.Equal(3, db.Source.Reads.Count(r => Equals(r.Parameters.Single(), ReviewPairSource.KeyB)));
        Assert.All(db.Source.Reads, read => Assert.DoesNotContain("private-r111", read.Text));
        Assert.All(db.Source.Reads, read => Assert.DoesNotContain("private-row", read.Text));
    }

    internal static async Task CatalogAsync(ReviewSecurityDatabase db)
    {
        var foreign = Assert.Single(await db.Raw.ForeignKeysAsync(ReviewPairSource.OwnerTable));
        Assert.Equal(ReviewPairSource.ForeignName, foreign.Name);
        Assert.Equal([ReviewPairSource.ForeignColumn], foreign.Columns);
        Assert.Equal(ReviewPairSource.ParentTable, foreign.ReferencedTable);
        Assert.Equal([ReviewPairSource.KeyColumn], foreign.ReferencedColumns);
        await using var connection = await db.Raw.Context.DataSource.OpenConnectionAsync();
        var indexes = await db.Raw.Context.Provider.CreateCatalog(connection).IndexesAsync(ReviewPairSource.OwnerTable);
        var unique = Assert.Single(indexes, i => i.Name == ReviewPairSource.UniqueName);
        Assert.True(unique.Unique);
        Assert.False(unique.Partial);
        Assert.Equal([ReviewPairSource.ForeignColumn], unique.Terms);
    }

    internal static void MalformedRefusesWithoutSql(ReviewSecurityDatabase db)
    {
        var before = db.Source.Reads.Count;
        var statements = db.Sink.Statements.Count;
        var parent = EntityDescriptor.Of("P", "p", true).Property("Id", "id", ColumnType.Text, false, true);
        var owner = EntityDescriptor.Of("C", "c", true).Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Text, true, false)
            .OneToOne(new(new("C", "Absent", "parent_id"), new("P", "id", "Parent", "Child"),
                new("fk", "uq", ReferentialAction.Restrict)));
        var error = Assert.Throws<OneToOneDeclarationException>(
            () => OrmSchema.Of("Malformed", owner, parent).Baseline());
        Assert.Equal(OneToOneError.UnmappedForeignKey, error.Error);
        SafeMessage(error.Message);
        Assert.Equal(before, db.Source.Reads.Count);
        Assert.Equal(statements, db.Sink.Statements.Count);
    }

    internal static void PrivateDiagnostics(ReviewSecurityDatabase db)
    {
        Assert.True(db.ParametersDefaultOff);
        Assert.NotEmpty(db.Sink.Statements);
        Assert.All(db.Sink.Statements, statement => Assert.Null(statement.Parameters));
        var text = JsonSerializer.Serialize(db.Sink.Statements.Select(s =>
            new { s.Text, s.Parameters, Error = s.Error?.ToString() }));
        foreach (var marker in PrivateMarkers())
        {
            Assert.DoesNotContain(marker, text);
            Assert.DoesNotContain(JsonSerializer.Serialize(marker).Trim('"'), text);
        }
        Assert.Contains(db.Sink.Statements, s => s.ParameterCount > 0);
        Assert.Contains(db.Sink.Statements, s => s.Error is not null);
    }

    internal static void SafeMessage(string message)
    {
        Assert.All(PrivateMarkers(), marker => Assert.DoesNotContain(marker, message));
        Assert.DoesNotContain("SELECT ", message);
        Assert.DoesNotContain("DROP TABLE", message);
        Assert.DoesNotContain("password=", message);
    }

    private static string[] PrivateMarkers() => [ReviewPairSource.KeyA, ReviewPairSource.KeyB,
        ReviewPairSource.RowA, ReviewPairSource.RowB, ReviewPairSource.OwnerRow];
}
