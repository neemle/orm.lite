using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests;

public sealed class InsertManyChunkingTests : IDisposable
{
    private const int Capacity = 9;
    private const int Width = 3;
    private const int PerChunk = Capacity / Width;

    private readonly string _path = Path.Combine(Path.GetTempPath(), "insertmany-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly StatementRecorder _recorder = new();

    static InsertManyChunkingTests() =>
        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, ["id", "name", "rank"]);

    public InsertManyChunkingTests()
    {
        using var connection = new SqliteConnection("Data Source=" + _path + ";Pooling=False");
        connection.Open();
        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE fixture_widgets (id TEXT PRIMARY KEY, name TEXT NOT NULL, rank INTEGER NOT NULL)";
        create.ExecuteNonQuery();
    }

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_path)!, Path.GetFileName(_path) + "*"))
            File.Delete(file);
    }

    private static string Col(string property) => property.ToLowerInvariant();

    private static FixtureWidget Map(DbDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt32(2));

    private CountingSqliteDataSource Source() => new(_path, _recorder);

    private static OrmContext Ctx(DbDataSource source, int capacity = Capacity) =>
        TestContext.Of(source, new BindLimitProvider(FixtureProvider.Instance, capacity));

    private static Query<FixtureWidget> Widgets(OrmContext ctx) => new(ctx, Map);

    private static Row<FixtureWidget> Widget(Guid id, int rank) =>
        new Row<FixtureWidget>().With(w => w.Id, id).With(w => w.Name, "w" + rank).With(w => w.Rank, rank);

    private static List<Row<FixtureWidget>> Rows(int count) =>
        Enumerable.Range(0, count).Select(i => Widget(Guid.NewGuid(), i)).ToList();

    private List<long> StoredRanks()
    {
        using var connection = new SqliteConnection("Data Source=" + _path + ";Pooling=False");
        connection.Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT rank FROM fixture_widgets ORDER BY rowid";
        using var reader = select.ExecuteReader();
        var ranks = new List<long>();
        while (reader.Read()) ranks.Add(reader.GetInt64(0));
        return ranks;
    }

    [Theory(DisplayName = "InsertManyAsync runs ceil(N / floor(C / k)) statements of at most C parameters, sums the affected rows and keeps the input order")]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(10, 4)]
    public async Task Chunk_math_holds_at_every_boundary(int rows, int statements)
    {
        var affected = await Widgets(Ctx(Source())).InsertManyAsync(Rows(rows));

        Assert.Equal(rows, affected);
        var recorded = _recorder.Statements;
        Assert.Equal(statements, recorded.Count);
        Assert.All(recorded, s => Assert.InRange(s.Values.Count, Width, Capacity));
        Assert.All(recorded, s => Assert.Equal(0, s.Values.Count % Width));
        Assert.Equal(rows * Width, recorded.Sum(s => s.Values.Count));
        Assert.Equal(Enumerable.Range(0, rows).Select(i => (long)i), StoredRanks());
    }

    [Fact(DisplayName = "a row set that fits one chunk executes the exact statement text and parameter list RenderInsertMany emits")]
    public async Task A_fitting_set_is_the_single_statement_render_insert_many_emits()
    {
        var rows = Rows(PerChunk);
        var ctx = Ctx(Source());
        var (sql, ps) = Widgets(ctx).RenderInsertMany(rows);

        await Widgets(ctx).InsertManyAsync(rows);

        var only = Assert.Single(_recorder.Statements);
        Assert.Equal(sql, only.Sql);
        Assert.Equal(ps, only.Values);
        Assert.False(only.InTransaction);
    }

    [Fact(DisplayName = "an empty row list keeps the needs-at-least-one-row refusal and sends nothing")]
    public async Task An_empty_list_is_refused_with_nothing_sent()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Widgets(Ctx(Source())).InsertManyAsync(Array.Empty<Row<FixtureWidget>>()));

        Assert.Contains("at least one row", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_recorder.Statements);
    }

    [Fact(DisplayName = "a row binding more columns than the dialect capacity is refused naming the capacity before any statement is sent")]
    public async Task A_row_wider_than_the_capacity_is_refused_before_anything_is_sent()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Widgets(Ctx(Source(), capacity: Width - 1)).InsertManyAsync(Rows(4)));

        Assert.Contains("capacity of " + (Width - 1), ex.Message, StringComparison.Ordinal);
        Assert.Empty(_recorder.Statements);
        Assert.Empty(StoredRanks());
    }

    [Fact(DisplayName = "a non-uniform row in the LAST chunk is refused with nothing sent because every chunk is rendered before the first is executed")]
    public async Task A_non_uniform_last_chunk_is_refused_before_the_first_statement()
    {
        var rows = Rows(8);
        rows[7] = new Row<FixtureWidget>().With(w => w.Id, Guid.NewGuid()).With(w => w.Name, "odd");

        await Assert.ThrowsAsync<ArgumentException>(() => Widgets(Ctx(Source())).InsertManyAsync(rows));

        Assert.Empty(_recorder.Statements);
        Assert.Empty(StoredRanks());
    }

    [Fact(DisplayName = "a whole later chunk naming a different column set is refused, not inserted under its own column list")]
    public async Task A_later_chunk_with_a_different_column_set_is_refused()
    {
        var rows = Rows(PerChunk).Concat(
            Enumerable.Range(0, PerChunk).Select(i =>
                new Row<FixtureWidget>().With(w => w.Id, Guid.NewGuid()).With(w => w.Name, "odd" + i))).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => Widgets(Ctx(Source())).InsertManyAsync(rows));

        Assert.Empty(_recorder.Statements);
        Assert.Empty(StoredRanks());
    }

    [Fact(DisplayName = "chunks of a bound unit of work all join its transaction and an uncommitted dispose leaves zero rows")]
    public async Task An_uncommitted_unit_of_work_rolls_back_every_chunk()
    {
        var ctx = Ctx(Source());
        await using (var uow = await UnitOfWork.BeginAsync(ctx))
        {
            var affected = await Widgets(ctx).On(uow).InsertManyAsync(Rows(10));
            Assert.Equal(10, affected);
        }

        var recorded = _recorder.Statements;
        Assert.Equal(4, recorded.Count);
        Assert.All(recorded, s => Assert.True(s.InTransaction));
        Assert.Equal(1, _recorder.ConnectionsCreated);
        Assert.Empty(StoredRanks());
    }

    [Fact(DisplayName = "chunks of a committed unit of work are all visible after the commit")]
    public async Task A_committed_unit_of_work_lands_every_chunk()
    {
        var ctx = Ctx(Source());
        await using (var uow = await UnitOfWork.BeginAsync(ctx))
        {
            await Widgets(ctx).On(uow).InsertManyAsync(Rows(10));
            await uow.CommitAsync();
        }

        Assert.Equal(Enumerable.Range(0, 10).Select(i => (long)i), StoredRanks());
    }

    [Fact(DisplayName = "an unbound multi-chunk insert opens its own transaction, so a failure on the second chunk leaves none of the first chunk's rows")]
    public async Task An_unbound_failure_on_the_second_chunk_rolls_back_the_first()
    {
        var rows = Rows(4);
        var duplicated = Guid.NewGuid();
        rows[0] = Widget(duplicated, 0);
        rows[PerChunk] = Widget(duplicated, PerChunk);

        await Assert.ThrowsAnyAsync<SqliteException>(() => Widgets(Ctx(Source())).InsertManyAsync(rows));

        var recorded = _recorder.Statements;
        Assert.Equal(2, recorded.Count);
        Assert.Single(recorded.Select(s => s.ConnectionId).Distinct());
        Assert.All(recorded, s => Assert.True(s.InTransaction));
        Assert.Empty(StoredRanks());
    }

    [Fact(DisplayName = "a bound connection without a transaction runs every chunk inside one transaction begun on that connection")]
    public async Task A_bound_connection_without_a_transaction_gets_one_transaction_for_all_chunks()
    {
        var source = Source();
        await using var connection = await source.OpenConnectionAsync();

        var affected = await Widgets(Ctx(source)).On(connection).InsertManyAsync(Rows(10));

        Assert.Equal(10, affected);
        var recorded = _recorder.Statements;
        Assert.Equal(4, recorded.Count);
        Assert.All(recorded, s => Assert.True(s.InTransaction));
        Assert.Equal(1, _recorder.ConnectionsCreated);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => (long)i), StoredRanks());
    }

    [Fact(DisplayName = "the PostgreSQL provider reports the protocol's 65535 bind parameters per statement")]
    public void Postgres_provider_capacity_is_the_protocol_limit()
    {
        Assert.Equal(65535, PostgresProvider.Instance.MaxBindParameters);
    }
}
