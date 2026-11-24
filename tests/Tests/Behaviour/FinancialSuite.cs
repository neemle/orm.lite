using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class FinancialSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    private static readonly HashSet<string> Prepared = [];

    private OrmContext Ctx => fixture.Context;

    private bool OnSqlite => Ctx.Provider.Id == "sqlite";

    private Query<FinValue> Values(UnitOfWork? work = null) => Of(new Query<FinValue>(Ctx, FinValue.Map), work);

    private Query<FinAccount> Accounts(UnitOfWork? work = null) => Of(new Query<FinAccount>(Ctx, FinAccount.Map), work);

    private Query<FinEntry> Entries(UnitOfWork? work = null) => Of(new Query<FinEntry>(Ctx, FinEntry.Map), work);

    private static Query<T> Of<T>(Query<T> query, UnitOfWork? work) where T : class => work is null ? query : query.On(work);

    private async Task PrepareAsync()
    {
        fixture.RequireAvailable();
        FinWorld.Register();
        await SchemaGate.WaitAsync();
        try
        {
            if (!Prepared.Add(Ctx.Provider.Id + ":" + fixture.GetHashCode())) return;
            await MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider,
            [
                new CreateTable(FinValue.Table,
                [
                    new ColumnDef("id", new TypeRef(ColumnType.Uuid), false, true),
                    new ColumnDef("grp", new TypeRef(ColumnType.Uuid), false, false),
                    new ColumnDef("v", new TypeRef(ColumnType.Decimal, 28, 4), false, false),
                    new ColumnDef("at", new TypeRef(ColumnType.Timestamp), true, false),
                ]),
                new CreateTable(FinAccount.Table,
                [
                    new ColumnDef("id", new TypeRef(ColumnType.Uuid), false, true),
                    new ColumnDef("ledger", new TypeRef(ColumnType.Uuid), false, false),
                    new ColumnDef("balance", new TypeRef(ColumnType.Decimal, 19, 4), false, false),
                ]),
                new CreateTable(FinEntry.Table,
                [
                    new ColumnDef("id", new TypeRef(ColumnType.Uuid), false, true),
                    new ColumnDef("ledger", new TypeRef(ColumnType.Uuid), false, false),
                    new ColumnDef("account_id", new TypeRef(ColumnType.Uuid), false, false),
                    new ColumnDef("amount", new TypeRef(ColumnType.Decimal, 19, 4), false, false),
                ]),
                new CreateTable("fin_unique", [new ColumnDef("v", new TypeRef(ColumnType.Decimal, 19, 4), false, false)]),
                new CreateIndex("fin_unique_v", "fin_unique", [new ColumnRef("v")], true, null),
            ]);
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private static Row<FinValue> ValueRow(Guid group, decimal value, DateTime? at = null) =>
        new Row<FinValue>().With(r => r.Id, Guid.NewGuid()).With(r => r.Grp, group).With(r => r.V, value).With(r => r.At, at);

    private async Task<Guid> StoreAsync(params decimal[] values)
    {
        var group = Guid.NewGuid();
        await Values().InsertManyAsync([.. values.Select(v => ValueRow(group, v))]);
        return group;
    }

    private static decimal Number(object? value) => value switch
    {
        decimal exact => exact,
        string text => decimal.Parse(text, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("the aggregate is not a decimal: " + value),
    };

    [SkippableFact(DisplayName = "[FIN] a decimal that a double cannot hold reads back digit for digit, with no float conversion on the way in or out")]
    public async Task Values_round_trip_exactly()
    {
        await PrepareAsync();
        decimal[] values = [0.1m, 0.2m, 0.3m, 12345678901234567890.1234m, -9999999999999999999999.9999m, 0.0001m];
        var group = await StoreAsync(values);

        var read = (await Values().Where(r => r.Grp == group).ToListAsync()).Select(r => r.V).Order().ToList();

        Assert.Equal(values.Order().ToList(), read);
        Assert.Equal("12345678901234567890.1234", read.Single(v => v > 1m).ToString(CultureInfo.InvariantCulture).TrimEnd('0'));
    }

    [SkippableFact(DisplayName = "[FIN] 0.1 + 0.2 summed in the database is exactly 0.3, and ten times 0.1 is exactly 1, where a double gives 0.30000000000000004")]
    public async Task Sum_is_exact()
    {
        await PrepareAsync();
        var pair = await StoreAsync(0.1m, 0.2m);
        var tenths = await StoreAsync([.. Enumerable.Repeat(0.1m, 10)]);
        var large = await StoreAsync(12345678901234567890.1234m, 12345678901234567890.1234m, 0.0001m);

        var sumPair = Number((await Values().Where(r => r.Grp == pair).AggregateSum(r => r.V).AggregatesAsync())[0]);
        var sumTenths = Number((await Values().Where(r => r.Grp == tenths).AggregateSum(r => r.V).AggregatesAsync())[0]);
        var sumLarge = Number((await Values().Where(r => r.Grp == large).AggregateSum(r => r.V).AggregatesAsync())[0]);

        Assert.Equal(0.3m, sumPair);
        Assert.Equal(1m, sumTenths);
        Assert.Equal(24691357802469135780.2469m, sumLarge);
        Assert.NotEqual(0.3, 0.1 + 0.2);
    }

    [SkippableFact(DisplayName = "[FIN] the sum of no rows is 0, the minimum is numeric, and SumAsync refuses a decimal column rather than rounding it to a long")]
    public async Task Sum_of_nothing_and_minimum()
    {
        await PrepareAsync();
        var group = await StoreAsync(10m, 9.99m, 2m, -3m, 0.0001m);

        var none = Guid.NewGuid();
        var empty = await Values().Where(r => r.Grp == none).AggregateSum(r => r.V).AggregatesAsync();
        var minimum = await Values().Where(r => r.Grp == group).AggregateMin(r => r.V).AggregatesAsync();

        Assert.Equal(0m, Number(empty[0]));
        Assert.Equal(-3m, Number(minimum[0]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Values().Where(r => r.Grp == group).SumAsync(r => r.V));
    }

    [SkippableFact(DisplayName = "[FIN] ordering and comparison are numeric: 10 sorts after 9.99 and 2, and a value written as 0.30 equals a parameter 0.3")]
    public async Task Order_and_comparison_are_numeric()
    {
        await PrepareAsync();
        var group = await StoreAsync(10m, 9.99m, 2m, -3m, 0.0001m, 0.30m, 100m);

        var ascending = (await Values().Where(r => r.Grp == group).OrderBy(r => r.V).ToListAsync()).Select(r => r.V).ToList();
        var descending = (await Values().Where(r => r.Grp == group).OrderByDescending(r => r.V).Take(2).ToListAsync()).Select(r => r.V).ToList();
        var equal = await Values().Where(r => r.Grp == group && r.V == 0.3m).CountAsync();
        var between = (await Values().Where(r => r.Grp == group && r.V > 2m && r.V < 10m).ToListAsync()).Select(r => r.V).ToList();
        var inList = await Values().Where(r => r.Grp == group && (r.V == 10m || r.V == 100m)).CountAsync();

        Assert.Equal([-3m, 0.0001m, 0.3m, 2m, 9.99m, 10m, 100m], ascending);
        Assert.Equal([100m, 10m], descending);
        Assert.Equal(1L, equal);
        Assert.Equal([9.99m], between);
        Assert.Equal(2L, inList);
    }

    [SkippableFact(DisplayName = "[FIN] a unique index treats 1.5 and 1.50 as the same value and refuses the second")]
    public async Task Unique_index_compares_numerically()
    {
        await PrepareAsync();
        var suffix = Guid.NewGuid().ToString("N");
        await MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider,
        [
            new CreateTable("fin_unique_" + suffix, [new ColumnDef("v", new TypeRef(ColumnType.Decimal, 19, 4), false, false)]),
            new CreateIndex("fin_unique_" + suffix + "_v", "fin_unique_" + suffix, [new ColumnRef("v")], true, null),
        ]);

        await fixture.ExecuteAsync("INSERT INTO fin_unique_" + suffix + " VALUES ('1.5')");
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.ExecuteAsync("INSERT INTO fin_unique_" + suffix + " VALUES ('1.50')"));
        await fixture.ExecuteAsync("INSERT INTO fin_unique_" + suffix + " VALUES ('1.51')");

        Assert.Equal(2L, Convert.ToInt64((await fixture.RowsAsync("SELECT count(*) FROM fin_unique_" + suffix))[0][0], CultureInfo.InvariantCulture));
    }

    [SkippableFact(DisplayName = "[FIN] a value past the declared precision is an error on both providers and stores nothing")]
    public async Task Overflow_is_an_error()
    {
        await PrepareAsync();
        var group = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<DbException>(() => Values().InsertManyAsync([ValueRow(group, 1_000_000_000_000_000_000_000_000m)]));
        await Assert.ThrowsAnyAsync<DbException>(() => Values().InsertManyAsync([ValueRow(group, decimal.MaxValue)]));

        Assert.Equal(0L, await Values().Where(r => r.Grp == group).CountAsync());
    }

    [SkippableFact(DisplayName = "[FIN] a value past the declared scale is pinned per provider: PostgreSQL rounds half away from zero, SQLite refuses and stores nothing")]
    public async Task Excess_scale_is_pinned()
    {
        await PrepareAsync();
        var group = Guid.NewGuid();

        if (OnSqlite)
        {
            await Assert.ThrowsAnyAsync<DbException>(() => Values().InsertManyAsync([ValueRow(group, 1.00005m)]));
            Assert.Equal(0L, await Values().Where(r => r.Grp == group).CountAsync());
            return;
        }
        await Values().InsertManyAsync([ValueRow(group, 1.00005m), ValueRow(group, -1.00005m)]);

        var read = (await Values().Where(r => r.Grp == group).ToListAsync()).Select(r => r.V).Order().ToList();

        Assert.Equal([-1.0001m, 1.0001m], read);
    }

    [SkippableFact(DisplayName = "[FIN] a ledger timestamp written as UTC reads back as the same instant at the resolution both providers keep (a microsecond), and is never read back as an unspecified kind")]
    public async Task Timestamps_are_utc_instants()
    {
        await PrepareAsync();
        var utc = new DateTime(2026, 11, 17, 23, 59, 59, DateTimeKind.Utc).AddTicks(1234560);
        var group = Guid.NewGuid();
        await Values().InsertManyAsync([ValueRow(group, 1m, utc)]);

        var read = (await Values().Where(r => r.Grp == group).ToListAsync()).Single().At!.Value;

        Assert.Equal(utc, read.ToUniversalTime());
        Assert.NotEqual(DateTimeKind.Unspecified, read.Kind);
    }

    [SkippableFact(DisplayName = "[FIN] a transfer between two accounts commits both legs and keeps the total, and one that would overdraw rolls every leg back")]
    public async Task Transfer_commits_or_rolls_back()
    {
        await PrepareAsync();
        var ledger = Guid.NewGuid();
        Guid savings = Guid.NewGuid(), current = Guid.NewGuid();
        await Accounts().InsertManyAsync(
        [
            new Row<FinAccount>().With(a => a.Id, savings).With(a => a.Ledger, ledger).With(a => a.Balance, 100.10m),
            new Row<FinAccount>().With(a => a.Id, current).With(a => a.Ledger, ledger).With(a => a.Balance, 0.20m),
        ]);

        await TransferAsync(ledger, savings, current, 0.10m);
        await TransferAsync(ledger, savings, current, 33.3333m);
        var overdraw = await Assert.ThrowsAsync<InvalidOperationException>(() => TransferAsync(ledger, current, savings, 1000m));

        Assert.Equal("insufficient funds", overdraw.Message);
        Assert.Equal(66.6667m, (await Accounts().Where(a => a.Id == savings).FirstOrDefaultAsync())!.Balance);
        Assert.Equal(33.6333m, (await Accounts().Where(a => a.Id == current).FirstOrDefaultAsync())!.Balance);
        Assert.Equal(100.30m, await TotalAsync(ledger));
        Assert.Equal(4L, await Entries().Where(e => e.Ledger == ledger).CountAsync());
        Assert.Equal(0m, Number((await Entries().Where(e => e.Ledger == ledger).AggregateSum(e => e.Amount).AggregatesAsync())[0]));
    }

    [SkippableFact(DisplayName = "[FIN] a failure after the debit but before the commit leaves both balances and the entries untouched")]
    public async Task Failure_before_commit_rolls_back()
    {
        await PrepareAsync();
        var ledger = Guid.NewGuid();
        Guid from = Guid.NewGuid(), to = Guid.NewGuid();
        await Accounts().InsertManyAsync(
        [
            new Row<FinAccount>().With(a => a.Id, from).With(a => a.Ledger, ledger).With(a => a.Balance, 50m),
            new Row<FinAccount>().With(a => a.Id, to).With(a => a.Ledger, ledger).With(a => a.Balance, 0m),
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var work = await UnitOfWork.BeginAsync(Ctx);
            await Accounts(work).Where(a => a.Id == from).Set(a => a.Balance, 10m).UpdateAsync();
            await Entries(work).InsertManyAsync([new Row<FinEntry>().With(e => e.Id, Guid.NewGuid()).With(e => e.Ledger, ledger).With(e => e.AccountId, from).With(e => e.Amount, -40m)]);
            throw new InvalidOperationException("crash before commit");
        });

        Assert.Equal(50m, (await Accounts().Where(a => a.Id == from).FirstOrDefaultAsync())!.Balance);
        Assert.Equal(0L, await Entries().Where(e => e.Ledger == ledger).CountAsync());
    }

    private async Task<decimal> TotalAsync(Guid ledger, UnitOfWork? work = null) =>
        Number((await Accounts(work).Where(a => a.Ledger == ledger).AggregateSum(a => a.Balance).AggregatesAsync())[0]);

    private async Task TransferAsync(Guid ledger, Guid from, Guid to, decimal amount)
    {
        await using var work = await UnitOfWork.BeginAsync(Ctx);
        var before = await TotalAsync(ledger, work);
        var source = await Accounts(work).Where(a => a.Id == from).ForUpdate().FirstOrDefaultAsync();
        var target = await Accounts(work).Where(a => a.Id == to).ForUpdate().FirstOrDefaultAsync();
        if (source is null || target is null) throw new InvalidOperationException("unknown account");
        if (amount <= 0m || source.Balance < amount) throw new InvalidOperationException("insufficient funds");
        await Accounts(work).Where(a => a.Id == from).Set(a => a.Balance, source.Balance - amount).UpdateAsync();
        await Accounts(work).Where(a => a.Id == to).Set(a => a.Balance, target.Balance + amount).UpdateAsync();
        await Entries(work).InsertManyAsync(
        [
            new Row<FinEntry>().With(e => e.Id, Guid.NewGuid()).With(e => e.Ledger, ledger).With(e => e.AccountId, from).With(e => e.Amount, -amount),
            new Row<FinEntry>().With(e => e.Id, Guid.NewGuid()).With(e => e.Ledger, ledger).With(e => e.AccountId, to).With(e => e.Amount, amount),
        ]);
        if (await TotalAsync(ledger, work) != before) throw new InvalidOperationException("the transfer changed the total");
        await work.CommitAsync();
    }
}

public sealed class SqliteFinancialTests(SqliteFixture fixture) : FinancialSuite<SqliteFixture>(fixture);

public sealed class PostgresFinancialTests(PostgresFixture fixture) : FinancialSuite<PostgresFixture>(fixture);
