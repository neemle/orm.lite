using Xunit;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed class RowLockRefusalTests
{
    private static Query<ClausePlain> Locked(string provider, string rowLock) =>
        ClauseTargets.Plain.Apply(ClauseTargets.Plain.New(provider).Where(x => x.Name == "n"), rowLock);

    [Theory(
        DisplayName =
            "[SEC] CountAsync, AnyAsync and SumAsync refuse a row lock by operation and lock name, and run no " +
            "statement")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public async Task Count_any_and_sum_refuse_a_row_lock(string provider, string rowLock)
    {
        var count = await Assert.ThrowsAsync<InvalidOperationException>(() => Locked(provider, rowLock).CountAsync());
        var any = await Assert.ThrowsAsync<InvalidOperationException>(() => Locked(provider, rowLock).AnyAsync());
        var sum = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Locked(provider, rowLock).SumAsync(x => x.Rank));

        LockNames.Verify(count.Message, "CountAsync");
        LockNames.Verify(any.Message, "AnyAsync");
        LockNames.Verify(sum.Message, "SumAsync");
        Assert.DoesNotContain("CountAsync", any.Message, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName =
            "[SEC] the aggregate row refuses a row lock through Compile, Render and the async form, naming " +
            "AggregatesAsync")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public async Task Aggregates_refuse_a_row_lock_in_every_form(string provider, string rowLock)
    {
        var query = Locked(provider, rowLock).AggregateCount().AggregateSum(x => x.Rank);

        var compiled = Assert.Throws<InvalidOperationException>(() => query.CompileAggregates());
        var rendered = Assert.Throws<InvalidOperationException>(() => query.RenderAggregates());
        var asynchronous = await Assert.ThrowsAsync<InvalidOperationException>(() => query.AggregatesAsync());

        LockNames.Verify(compiled.Message, "AggregatesAsync");
        LockNames.Verify(rendered.Message, "AggregatesAsync");
        LockNames.Verify(asynchronous.Message, "AggregatesAsync");
    }

    [Theory(
        DisplayName =
            "[SEC] the scalar read refuses a row lock through Compile, Render and the async form, naming " +
            "ScalarAsync, with an order and a page set on the query left out of the refusal")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public async Task Scalar_refuses_a_row_lock_in_every_form(string provider, string rowLock)
    {
        var query = Locked(provider, rowLock).OrderBy(x => x.Rank).Skip(1).Take(3);

        var compiled = Assert.Throws<InvalidOperationException>(() => query.CompileScalar(x => x.Name));
        var rendered = Assert.Throws<InvalidOperationException>(() => query.RenderScalar(x => x.Name));
        var asynchronous = await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.ScalarAsync<string>(x => x.Name));

        LockNames.Verify(compiled.Message, "ScalarAsync");
        LockNames.Verify(rendered.Message, "ScalarAsync");
        LockNames.Verify(asynchronous.Message, "ScalarAsync");
    }

    [Theory(
        DisplayName =
            "[SEC] a write refuses a row lock in Compile and Render, naming the operation and the lock")]
    [MemberData(nameof(LockMatrix.Writes), MemberType = typeof(LockMatrix))]
    public void A_write_with_a_row_lock_is_refused(
        string provider, string entity, string operation, string rowLock, bool lockFirst)
    {
        var shape = new ClauseShape(provider, operation, rowLock, lockFirst);
        var target = ClauseTargets.Of(entity);

        var compiled = Assert.Throws<InvalidOperationException>(() => target.Compile(shape));
        var rendered = Assert.Throws<InvalidOperationException>(() => target.Render(shape));

        LockNames.Verify(compiled.Message, operation);
        LockNames.Verify(rendered.Message, operation);
    }

    [Theory(
        DisplayName =
            "[SEC] GroupBy refuses a query that already holds a row lock, on one, two and three keys")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public void GroupBy_refuses_a_row_lock_already_set(string provider, string rowLock)
    {
        var one = Assert.Throws<InvalidOperationException>(() => Locked(provider, rowLock).GroupBy(x => x.Name));
        var two = Assert.Throws<InvalidOperationException>(
            () => Locked(provider, rowLock).GroupBy(x => x.Name, x => x.Rank));
        var three = Assert.Throws<InvalidOperationException>(
            () => Locked(provider, rowLock).GroupBy(x => x.Name, x => x.Rank, x => x.Id));

        LockNames.Verify(one.Message, "GroupBy");
        LockNames.Verify(two.Message, "GroupBy");
        LockNames.Verify(three.Message, "GroupBy");
    }

    [Theory(
        DisplayName =
            "[SEC] a row lock added to the query AFTER GroupBy is refused at the grouping terminal, in Compile, " +
            "Render and the async form")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public async Task A_row_lock_added_after_GroupBy_is_refused_at_the_terminal(string provider, string rowLock)
    {
        var query = ClauseTargets.Plain.New(provider).Where(x => x.Name == "n");
        var grouping = query.GroupBy(x => x.Name);
        grouping.Count();
        ClauseTargets.Plain.Apply(query, rowLock);

        var compiled = Assert.Throws<InvalidOperationException>(() => grouping.CompileSelect());
        var rendered = Assert.Throws<InvalidOperationException>(() => grouping.RenderSelect());
        var asynchronous = await Assert.ThrowsAsync<InvalidOperationException>(() => grouping.ToListAsync());

        LockNames.Verify(compiled.Message, "GroupBy");
        LockNames.Verify(rendered.Message, "GroupBy");
        LockNames.Verify(asynchronous.Message, "GroupBy");
    }

    [Theory(
        DisplayName =
            "a row lock together with paging or an order names every refused clause in the fixed order, and an " +
            "order is named only where it is refused")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public async Task A_lock_with_other_clauses_names_each_refused_clause_once(string provider)
    {
        var aggregate = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Locked(provider, "ForUpdate").Take(5).Skip(2).OrderBy(x => x.Rank).CountAsync());
        var write = Assert.Throws<InvalidOperationException>(
            () => Locked(provider, "ForNoKeyUpdate").Take(5).OrderBy(x => x.Rank).Set(x => x.Rank, 7).CompileUpdate());
        var scalar = Assert.Throws<InvalidOperationException>(
            () => Locked(provider, "ForUpdate").Take(5).OrderBy(x => x.Rank).CompileScalar(x => x.Name));

        LockNames.Verify(aggregate.Message, "CountAsync", "Take", "Skip");
        LockNames.Verify(write.Message, "UpdateAsync", "Take", "OrderBy");
        LockNames.Verify(scalar.Message, "ScalarAsync");
        Assert.Contains("Take, Skip, ForUpdate or ForNoKeyUpdate:", aggregate.Message, StringComparison.Ordinal);
        Assert.Contains("a query that pages its rows", aggregate.Message, StringComparison.Ordinal);
        Assert.Contains("the row lock set on the query would be dropped", aggregate.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "a decimal Sum plus a row lock still reports the decimal refusal first")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public async Task The_decimal_refusal_comes_before_the_lock(string provider, string rowLock)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Locked(provider, rowLock).SumAsync(x => x.Price));

        Assert.Contains("would round a decimal column", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "no aggregate term plus a row lock still reports the missing aggregate term")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public void The_missing_aggregate_term_comes_before_the_lock(string provider, string rowLock)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Locked(provider, rowLock).CompileAggregates());

        Assert.Contains("at least one aggregate term", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName =
            "a refused terminal leaves the query as it was: the locked read still renders its lock, and a " +
            "lock-free query answers every terminal as before")]
    [MemberData(nameof(LockMatrix.Every), MemberType = typeof(LockMatrix))]
    public void A_refusal_changes_nothing_and_a_locked_read_keeps_its_lock(string provider, string rowLock)
    {
        var query = Locked(provider, rowLock);
        Assert.Throws<InvalidOperationException>(() => query.RenderScalar(x => x.Rank));
        var expected = (provider, rowLock) switch
        {
            ("postgres", "ForUpdate") => " FOR UPDATE",
            ("postgres", _) => " FOR NO KEY UPDATE",
            _ => "",
        };

        Assert.Equal(
            "SELECT * FROM \"clause_plain\" WHERE \"name\" = @p0" + expected, query.RenderSelect());
        Assert.Equal(
            "SELECT \"rank\" FROM \"clause_plain\" WHERE \"name\" = @p0" + expected,
            query.RenderProject([x => x.Rank]));
        Assert.Equal(
            "SELECT \"rank\" FROM \"clause_plain\" WHERE \"name\" = @p0 LIMIT 1",
            ClauseTargets.Plain.New(provider).Where(x => x.Name == "n").RenderScalar(x => x.Rank));
    }
}
