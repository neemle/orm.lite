using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class IndexSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private bool Sqlite => Fixture.ProviderId == "sqlite";

    protected TFixture Fixture { get; } = fixture;

    protected OrmContext Ctx => Fixture.Context;

    private string Nocase => Sqlite ? "NOCASE" : "C";

    private static EntityDescriptor Items(string table, Func<EntityDescriptor, EntityDescriptor> indexes) =>
        indexes(EntityDescriptor.Of("Fixture." + table, table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Status", "status", ColumnType.Text, false, false)
            .Property("Priority", "priority", ColumnType.Int32, true, false)
            .Property("Title", "title", ColumnType.Text, true, false));

    private Task ApplyAsync(params Node[] operations) => MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, operations);

    private async Task CreateAsync(EntityDescriptor entity)
    {
        await ApplyAsync([.. EntityStatements.CreateTables(entity)]);
        await ReconcileAsync(entity);
    }

    private Task ReconcileAsync(EntityDescriptor entity) =>
        ApplyAsync(new ReconcileIndexes(NodeList.Of(EntityStatements.ReconcileIndexes(entity))));

    private async Task<string> DefinitionAsync(string table, string index)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var found = (await Ctx.Provider.CreateCatalog(connection).IndexesAsync(table)).Single(i => i.Name == index);
        return found.Definition.Replace("\"", "", StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<CatalogIndex>> IndexesAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return await Ctx.Provider.CreateCatalog(connection).IndexesAsync(table);
    }

    private async Task<int> CountAsync(string table) => Convert.ToInt32((await Fixture.RowsAsync("SELECT count(*) FROM \"" + table + "\""))[0][0]);

    private Task InsertAsync(string table, string id, string status, string? title = null, int? priority = null) =>
        Fixture.ExecuteAsync("INSERT INTO \"" + table + "\" (\"id\", \"status\", \"priority\", \"title\") VALUES ('" + id + "', '" + status + "', "
            + (priority?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL") + ", " + (title is null ? "NULL" : "'" + title + "'") + ")");

    private async Task FailsAsync(Task statement) => await Assert.ThrowsAnyAsync<DbException>(() => statement);

    [SkippableFact(DisplayName = "an index on one property keeps its direction, and the catalog reads the stored definition back")]
    public async Task A_single_column_index_with_a_direction()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_single", d => d.Index("ix_single_priority", false, false, IndexTerm.Desc("priority")));

        await CreateAsync(items);

        Assert.Contains("priority DESC", await DefinitionAsync("ix_single", "ix_single_priority"), StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "a composite index keeps a mixed direction column by column, and an ordered read over it returns the rows in that order")]
    public async Task A_composite_index_with_mixed_directions()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_mixed", d => d.Index("ix_mixed_status_priority", false, false, IndexTerm.Asc("status"), IndexTerm.Desc("priority")));

        await CreateAsync(items);
        await InsertAsync("ix_mixed", "1", "open", priority: 1);
        await InsertAsync("ix_mixed", "2", "open", priority: 3);
        await InsertAsync("ix_mixed", "3", "open", priority: 2);

        var definition = await DefinitionAsync("ix_mixed", "ix_mixed_status_priority");
        Assert.Contains("priority DESC", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("status DESC", definition, StringComparison.Ordinal);
        var ordered = await Fixture.RowsAsync("SELECT \"id\" FROM \"ix_mixed\" ORDER BY \"status\", \"priority\" DESC");
        Assert.Equal(["2", "3", "1"], ordered.Select(r => (string)r[0]!));
    }

    [SkippableFact(DisplayName = "a unique composite index under an explicit name keeps that name and refuses a duplicate")]
    public async Task A_unique_index_under_an_explicit_name()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_unique", d => d.Index("uq_custom_name", true, false, IndexTerm.Asc("status"), IndexTerm.Desc("title")));

        await CreateAsync(items);

        Assert.Equal(["uq_custom_name"], (await IndexesAsync("ix_unique")).Select(i => i.Name));
        Assert.True((await IndexesAsync("ix_unique")).Single().Unique);
        await InsertAsync("ix_unique", "1", "open", "a");
        await FailsAsync(InsertAsync("ix_unique", "2", "open", "a"));
    }

    [SkippableFact(DisplayName = "a collated term keeps its collation: NOCASE makes SQLite treat two spellings as one key, and the C collation on PostgreSQL is stored in the definition")]
    public async Task A_collated_term()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_collate", d => d.Index("ix_collate_title", true, false, IndexTerm.Asc("title").Collate(Nocase)));

        await CreateAsync(items);

        Assert.Contains("COLLATE", await DefinitionAsync("ix_collate", "ix_collate_title"), StringComparison.OrdinalIgnoreCase);
        await InsertAsync("ix_collate", "1", "open", "Alpha");
        if (Sqlite) await FailsAsync(InsertAsync("ix_collate", "2", "open", "ALPHA"));
        else await InsertAsync("ix_collate", "2", "open", "ALPHA");
    }

    [SkippableFact(DisplayName = "a collation the provider does not have is refused by name with nothing created")]
    public async Task An_unknown_sqlite_collation_is_refused_by_name()
    {
        Fixture.RequireAvailable();
        Skip.IfNot(Sqlite, "PostgreSQL accepts any collation name and the server refuses an unknown one.");
        var items = Items("ix_badcoll", d => d.Index("ix_badcoll_title", false, false, IndexTerm.Asc("title").Collate("klingon")));
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);

        var refusal = await Assert.ThrowsAsync<NotSupportedException>(() => ReconcileAsync(items));

        Assert.Contains("klingon", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await IndexesAsync("ix_badcoll"));
    }

    [SkippableFact(DisplayName = "a null placement in an index is created on PostgreSQL and refused by capability on SQLite, whose index grammar has no such clause, with nothing created")]
    public async Task A_null_placement_in_an_index()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_nulls", d => d.Index("ix_nulls_priority", false, false, IndexTerm.Desc("priority").NullsLast()));
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);

        if (!Sqlite)
        {
            await ReconcileAsync(items);
            Assert.Contains("priority DESC NULLS LAST", await DefinitionAsync("ix_nulls", "ix_nulls_priority"), StringComparison.Ordinal);
            return;
        }

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => ReconcileAsync(items));
        Assert.Equal(Capability.IndexNullsOrdering, refusal.Capability);
        Assert.Contains("ix_nulls_priority", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await IndexesAsync("ix_nulls"));
        await FailsAsync(Fixture.ExecuteAsync("CREATE INDEX \"ix_nulls_raw\" ON \"ix_nulls\" (\"priority\" DESC NULLS LAST)"));
    }

    [SkippableFact(DisplayName = "a partial index admits a duplicate outside its predicate and refuses it inside, and the catalog reports it as partial")]
    public async Task A_partial_unique_index()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_partial", d => d
            .Index("uq_partial_title", true, false, "title")
            .Filter(IndexCondition.Of("status", IndexFilterOp.Equal, "open")));

        await CreateAsync(items);

        Assert.True((await IndexesAsync("ix_partial")).Single().Partial);
        await InsertAsync("ix_partial", "1", "closed", "a");
        await InsertAsync("ix_partial", "2", "closed", "a");
        await InsertAsync("ix_partial", "3", "open", "a");
        await FailsAsync(InsertAsync("ix_partial", "4", "open", "a"));
    }

    [SkippableFact(DisplayName = "a unique index over lower(column) treats two spellings as one key")]
    public async Task A_lowered_expression_term()
    {
        Fixture.RequireAvailable();
        var items = Items("ix_lower", d => d.Index("uq_lower_title", true, false, IndexTerm.LowerOf("title")));

        await CreateAsync(items);

        Assert.Contains("lower(title)", await DefinitionAsync("ix_lower", "uq_lower_title"), StringComparison.Ordinal);
        await InsertAsync("ix_lower", "1", "open", "Alpha");
        await FailsAsync(InsertAsync("ix_lower", "2", "open", "ALPHA"));
    }

    [SkippableFact(DisplayName = "a changed direction makes the reconcile drop and recreate the index under the same name and keep every row; an unchanged declaration leaves it alone")]
    public async Task A_changed_direction_is_reconciled()
    {
        Fixture.RequireAvailable();
        var before = Items("ix_drift", d => d.Index("ix_drift_priority", false, false, IndexTerm.Asc("priority")));
        var after = Items("ix_drift", d => d.Index("ix_drift_priority", false, false, IndexTerm.Desc("priority")));
        await CreateAsync(before);
        await InsertAsync("ix_drift", "1", "open", priority: 1);
        var original = await DefinitionAsync("ix_drift", "ix_drift_priority");

        await ReconcileAsync(before);
        Assert.Equal(original, await DefinitionAsync("ix_drift", "ix_drift_priority"));
        await ReconcileAsync(after);

        Assert.DoesNotContain("DESC", original, StringComparison.Ordinal);
        Assert.Contains("priority DESC", await DefinitionAsync("ix_drift", "ix_drift_priority"), StringComparison.Ordinal);
        Assert.Equal(1, await CountAsync("ix_drift"));
    }

    [SkippableFact(DisplayName = "a changed uniqueness, a changed predicate and a changed collation are each reconciled in place")]
    public async Task A_changed_uniqueness_predicate_and_collation_are_reconciled()
    {
        Fixture.RequireAvailable();
        var plain = Items("ix_drift2", d => d.Index("ix_drift2_title", false, false, "title"));
        var unique = Items("ix_drift2", d => d.Index("ix_drift2_title", true, false, "title"));
        var partial = Items("ix_drift2", d => d.Index("ix_drift2_title", true, false, "title").Filter(IndexCondition.NotNull("priority")));
        var collated = Items("ix_drift2", d => d.Index("ix_drift2_title", true, false, IndexTerm.Asc("title").Collate(Nocase)).Filter(IndexCondition.NotNull("priority")));
        await CreateAsync(plain);
        Assert.False((await IndexesAsync("ix_drift2")).Single().Unique);

        await ReconcileAsync(unique);
        Assert.True((await IndexesAsync("ix_drift2")).Single().Unique);
        Assert.False((await IndexesAsync("ix_drift2")).Single().Partial);

        await ReconcileAsync(partial);
        Assert.True((await IndexesAsync("ix_drift2")).Single().Partial);

        await ReconcileAsync(collated);
        Assert.Contains("COLLATE", await DefinitionAsync("ix_drift2", "ix_drift2_title"), StringComparison.OrdinalIgnoreCase);
    }

    private static EntityDescriptor Money(string table, Func<EntityDescriptor, EntityDescriptor> indexes) =>
        indexes(EntityDescriptor.Of("Fixture." + table, table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Amount", "amount", ColumnType.Decimal, false, false, 19, 4));

    private Task InsertMoneyAsync(string table, string id, string amount) =>
        Fixture.ExecuteAsync("INSERT INTO \"" + table + "\" (\"id\", \"amount\") VALUES ('" + id + "', '" + amount + "')");

    [SkippableFact(DisplayName = "a unique descending index on a decimal column treats 1.5 and 1.50 as one key and orders numerically, on both providers")]
    public async Task An_index_on_a_decimal_column_is_numeric()
    {
        Fixture.RequireAvailable();
        var money = Money("ix_money", d => d.Index("uq_money_amount", true, false, IndexTerm.Desc("amount")));

        await CreateAsync(money);
        await InsertMoneyAsync("ix_money", "1", "1.5");
        await FailsAsync(InsertMoneyAsync("ix_money", "2", "1.50"));
        await InsertMoneyAsync("ix_money", "3", "10");
        await InsertMoneyAsync("ix_money", "4", "9");
        await InsertMoneyAsync("ix_money", "5", "-2.5");

        Assert.Contains("amount DESC", await DefinitionAsync("ix_money", "uq_money_amount"), StringComparison.Ordinal);
        Assert.DoesNotContain("COLLATE", await DefinitionAsync("ix_money", "uq_money_amount"), StringComparison.OrdinalIgnoreCase);
        const string ordered = "SELECT \"id\" FROM \"ix_money\" ORDER BY \"amount\" DESC";
        Assert.Equal(["3", "4", "1", "5"], (await Fixture.RowsAsync(ordered)).Select(r => (string)r[0]!));
        if (!Sqlite) return;
        var plan = string.Join("; ", (await Fixture.RowsAsync("EXPLAIN QUERY PLAN " + ordered)).Select(r => Convert.ToString(r[3], System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Contains("uq_money_amount", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("TEMP B-TREE", plan, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "a collation or lower() on a decimal column is refused with the column named and nothing created, from a declaration and from a hand-written index")]
    public async Task A_collation_on_a_decimal_column_is_refused()
    {
        Fixture.RequireAvailable();
        var collated = Money("ix_money_c", d => d.Index("ix_money_c_amount", false, false, IndexTerm.Asc("amount").Collate(Nocase)));
        var lowered = Money("ix_money_c", d => d.Index("ix_money_c_amount", false, false, IndexTerm.LowerOf("amount")));
        await ApplyAsync([.. EntityStatements.CreateTables(collated)]);

        var fromCollate = await Assert.ThrowsAsync<NotSupportedException>(() => ReconcileAsync(collated));
        var fromLower = await Assert.ThrowsAsync<NotSupportedException>(() => ReconcileAsync(lowered));
        Assert.Contains("amount", fromCollate.Message, StringComparison.Ordinal);
        Assert.Contains("decimal", fromCollate.Message, StringComparison.Ordinal);
        Assert.Contains("amount", fromLower.Message, StringComparison.Ordinal);
        var handWritten = new CreateIndex("ix_money_c_hand", "ix_money_c", NodeList.Of([IndexTerm.Asc("amount").Collate(Nocase).ToNode()]));
        if (Sqlite)
        {
            var refusal = await Assert.ThrowsAsync<NotSupportedException>(() => ApplyAsync(handWritten));
            Assert.Contains("orm_decimal", refusal.Message, StringComparison.Ordinal);
        }
        else
        {
            await Assert.ThrowsAnyAsync<DbException>(() => ApplyAsync(handWritten));
        }

        Assert.Empty(await IndexesAsync("ix_money_c"));
    }
}

public sealed class SqliteIndexTests(SqliteFixture fixture) : IndexSuite<SqliteFixture>(fixture);

public sealed class PostgresIndexTests(PostgresFixture fixture) : IndexSuite<PostgresFixture>(fixture);
