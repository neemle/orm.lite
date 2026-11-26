using System.Data;
using System.Data.Common;
using System.Reflection;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.OneToOne;

[Collection("Graph")]
public sealed class OneToOneReviewSecurityTests
{
    [Theory(DisplayName = "[UC-005][SEC][R2] generated quoted TEXT pair binds private values on the real provider")]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task Quoted_pair_keeps_exact_associations_and_private_diagnostics(string provider)
    {
        var (diagnostics, _, output) = Run(ReviewPairSource.Text);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        model.RunModuleInitializer();
        await using var db = await ReviewSecurityDatabase.OpenAsync(provider);
        await db.Raw.Context.MigrateAsync(model.Schema.Contributions());
        await db.Raw.ExecuteAsync("CREATE TABLE r111_witness(id INTEGER); INSERT INTO r111_witness VALUES(42)");
        await using var connection = await db.Context.DataSource.OpenConnectionAsync();
        var operations = model.Class("PrincipalOrm").Assembly.GetType("Fixture.ReviewOps", true)!;
        await Invoke<Task>(operations, "Seed", db.Context, connection);
        db.Sink.Statements.Clear();
        db.Source.Reads.Clear();
        Assert.True(await Invoke<Task<bool>>(operations, "Read", db.Context, connection));
        ReviewSecurityAssertions.BoundReads(db);
        await ReviewSecurityAssertions.CatalogAsync(db);
        ReviewSecurityAssertions.MalformedRefusesWithoutSql(db);
        await RefusalsAsync(db, connection, operations, provider);
        ReviewSecurityAssertions.PrivateDiagnostics(db);
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(42L, Convert.ToInt64(await db.Raw.ScalarAsync("SELECT id FROM r111_witness")));
    }

    private static T Invoke<T>(Type operations, string method, OrmContext db, DbConnection connection) =>
        Assert.IsAssignableFrom<T>(operations.GetMethod(method)!.Invoke(null, [db, connection]));

    private static async Task RefusalsAsync(ReviewSecurityDatabase db, DbConnection connection,
        Type operations, string provider)
    {
        var q = ReviewPairSource.Quote;
        await db.Raw.ExecuteAsync("DROP INDEX " + q(ReviewPairSource.UniqueName));
        await db.Raw.ExecuteAsync("INSERT INTO " + q(ReviewPairSource.OwnerTable) + "(id,"
            + q(ReviewPairSource.ForeignColumn) + "," + q(ReviewPairSource.RowColumn) + ") VALUES(@p0,@p1,@p2)",
            new Guid(3, 0, 0, new byte[8]), ReviewPairSource.KeyB, ReviewPairSource.OwnerRow);
        var cardinality = await Invoke<Task<Exception>>(operations, "Refuse", db.Context, connection);
        Assert.IsType<OneToOneCardinalityException>(cardinality);
        ReviewSecurityAssertions.SafeMessage(cardinality.Message);
        await db.Raw.ExecuteAsync("DROP TABLE " + q(ReviewPairSource.OwnerTable));
        var native = await Invoke<Task<Exception>>(operations, "Refuse", db.Context, connection);
        Assert.Equal(provider == "sqlite" ? typeof(Microsoft.Data.Sqlite.SqliteException)
            : typeof(Npgsql.PostgresException), native.GetType());
        Assert.Null(native.InnerException);
        Assert.Contains(db.Sink.Statements, statement => ReferenceEquals(native, statement.Error));
    }
}
