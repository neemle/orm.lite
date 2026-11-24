using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class SqliteJsonDifferenceTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly MovedFieldKey K = new("k");

    private Query<BehaveNote> Notes() => new(fixture.Context, BehaveNote.Map);

    private static Row<BehaveNote> DocRow(Guid tenant, string title) =>
        new Row<BehaveNote>().With(n => n.Id, Guid.NewGuid()).With(n => n.TenantId, tenant).With(n => n.Title, title).With(n => n.Stars, 1);

    [Fact(DisplayName = "difference: a document written outside the library is kept as written, not rewritten to the canonical form, yet every read, filter and merge treats it the same as the canonical one")]
    public async Task Raw_writes_are_not_normalized()
    {
        var tenant = Guid.NewGuid();
        await fixture.ExecuteAsync(
            $"INSERT INTO behave_notes (id, tenant_id, title, stars, doc) VALUES ('{Guid.NewGuid()}', '{tenant}', 'raw', 1, '{{\"b\":1,\"a\":[1, 2],\"dup\":1,\"dup\":2}}')");

        var stored = Assert.Single(await Notes().Where(n => n.TenantId == tenant).ToListAsync());
        var byKey = await Notes().Where(n => n.TenantId == tenant && Db.JsonKeyEquals(n.Doc, "dup", "2", JsonValueOrder.Text)).CountAsync();
        await Notes().Where(n => n.TenantId == tenant).SetJsonMerge(n => n.Doc, Jsonb.Parse("""{"c":3}""")).UpdateAsync();
        var merged = Assert.Single(await Notes().Where(n => n.TenantId == tenant).ToListAsync());

        Assert.Equal("""{"b":1,"a":[1, 2],"dup":1,"dup":2}""", stored.Doc!.Json);
        Assert.Equal(1, byKey);
        Assert.Equal("""{"a": [1, 2], "b": 1, "c": 3, "dup": 2}""", merged.Doc!.Json);
    }

    [Fact(DisplayName = "difference: a parameter cast to text binds the text of a boolean and of a decimal, but a double keeps SQLite's own spelling, so 1.0 does not match a stored 1")]
    public async Task A_double_parameter_keeps_the_sqlite_spelling()
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync([DocRow(tenant, "d1").With(n => n.Doc, Jsonb.Parse("""{"k":1}"""))]);

        var integer = await Notes().Where(n => n.TenantId == tenant && Db.MovedFieldEquals(n.Doc, K, 1)).CountAsync();
        var real = await Notes().Where(n => n.TenantId == tenant && Db.MovedFieldEquals(n.Doc, K, 1.0)).CountAsync();

        Assert.Equal(1, integer);
        Assert.Equal(0, real);
    }

    [Fact(DisplayName = "difference: exotic numeric spellings follow SQLite's reading, so a hexadecimal float and NaN read as no number, where PostgreSQL's double precision reads the first and numeric reads the second")]
    public void Exotic_spellings_are_not_numbers()
    {
        Assert.Null(SqliteJsonInput.Float8("0x1.8p3"));
        Assert.Null(SqliteJsonInput.Numeric("NaN"));
        Assert.Null(SqliteJsonInput.Float8("NaN"));
        Assert.Equal(double.PositiveInfinity, SqliteJsonInput.Numeric("Infinity"));
        Assert.Equal(11d, SqliteJsonInput.Float8("0xB"));
    }

    [Fact(DisplayName = "difference: an object built from a SQLite value takes the number, text or null its storage class says, so a boolean the host meant is a number, and an infinity is the text PostgreSQL also writes")]
    public void Object_values_follow_the_storage_class()
    {
        Assert.Equal("""{"k": 1}""", SqliteJsonFunctions.BuildObject("k", 1L));
        Assert.Equal("""{"k": "Infinity"}""", SqliteJsonFunctions.BuildObject("k", double.PositiveInfinity));
        Assert.Equal("""{"k": null}""", SqliteJsonFunctions.BuildObject("k", DBNull.Value));
        Assert.Throws<InvalidOperationException>(() => SqliteJsonFunctions.BuildObject("k", new byte[] { 1 }));
        Assert.Throws<ArgumentException>(() => SqliteJsonFunctions.BuildObject(null, 1L));
    }

    [Fact(DisplayName = "difference: the registered functions exist only on connections the library opened, so a statement the library wrote cannot run on a bare connection to the same file, while the column check alone still protects the data")]
    public async Task Bare_connections_have_no_json_functions()
    {
        await using var bare = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + fixture.DatabasePath);
        await bare.OpenAsync();
        await using var command = bare.CreateCommand();
        command.CommandText = "SELECT orm_json_text('{}', 'k')";

        var error = await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => command.ExecuteScalarAsync());

        Assert.Contains("orm_json_text", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "difference: text order is SQLite's byte order, the order PostgreSQL gives under a C collation, so uppercase sorts before lowercase and an accented letter after z")]
    public async Task Text_order_is_bytewise()
    {
        var tenant = Guid.NewGuid();
        foreach (var (title, value) in new[] { ("o1", "b"), ("o2", "B"), ("o3", "é"), ("o4", "z"), ("o5", "a") })
            await Notes().InsertManyAsync([DocRow(tenant, title).With(n => n.Doc, Jsonb.Parse("{\"k\":\"" + value + "\"}"))]);

        var ordered = await Notes().Where(n => n.TenantId == tenant).OrderByJsonKey(n => n.Doc, new JsonKey("k"), false, false).ToListAsync();

        Assert.Equal(["o2", "o5", "o1", "o4", "o3"], ordered.Select(n => n.Title));
    }
}
