using Neemle.OrmLite.Tests.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph.Security;

[Collection("Graph")]
public sealed class GraphHostileReadTests
{
    [Theory(DisplayName = "[SEC][UC-002] SQLite quoted graph endpoints and private bound keys stay separate")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sqlite_round_trip_preserves_exact_associations_and_private_diagnostics(bool logParameters)
    {
        await using var db = await SecurityDatabase.SqliteAsync(logParameters);
        await VerifyAsync(db, logParameters);
    }

    [PostgresFact(DisplayName = "[SEC][UC-002] PostgreSQL quoted graph endpoints and bound private keys stay separate")]
    public async Task Postgres_round_trip_has_the_same_membership_and_privacy_in_both_log_modes()
    {
        foreach (var logParameters in new[] { false, true })
        {
            await using var db = await SecurityDatabase.PostgresAsync(logParameters);
            await VerifyAsync(db, logParameters);
        }
    }

    private static async Task VerifyAsync(SecurityDatabase db, bool logParameters)
    {
        Assert.Contains(SecurityModel.Credential, db.Source.ConnectionString);
        var request = new GraphRequest<SecurityRoot>(GraphDomain.Limits(),
            SecurityModel.Children.Then(SecurityModel.Parent), SecurityModel.Peers);
        var roots = await SecurityModel.Query(db.Context).ToGraphListAsync(request);
        SecurityAssertions.Membership(roots);
        SecurityAssertions.BoundValues(db);
        SecurityAssertions.Privacy(db, logParameters);
        await db.AssertWitnessAsync();
        Assert.True(Assert.Single(db.Source.Connections).WasDisposed);
        Assert.All(db.Source.Commands, command => Assert.True(command.WasDisposed));
        Assert.All(db.Source.Readers, reader => Assert.True(reader.IsClosed));
    }
}
