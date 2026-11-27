using System.Data.Common;
using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class BehaviourSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    protected OrmContext Ctx => Fixture.Context;

    protected Query<BehaveNote> Notes(UnitOfWork? work = null)
    {
        var query = new Query<BehaveNote>(Ctx, BehaveNote.Map);
        return work is null ? query : query.On(work);
    }

    protected Query<BehaveCounter> Counters(UnitOfWork? work = null)
    {
        var query = new Query<BehaveCounter>(Ctx, BehaveCounter.Map);
        return work is null ? query : query.On(work);
    }

    protected static Row<BehaveNote> NoteRow(Guid tenant, string title, int stars = 1, decimal? price = null, Guid? id = null) =>
        new Row<BehaveNote>()
            .With(n => n.Id, id ?? Guid.NewGuid())
            .With(n => n.TenantId, tenant)
            .With(n => n.Title, title)
            .With(n => n.Stars, stars)
            .With(n => n.Price, price);

    protected async Task<List<BehaveNote>> SeedAsync(Guid tenant, params (string Title, int Stars)[] notes)
    {
        var rows = notes.Select(n => NoteRow(tenant, n.Title, n.Stars)).ToList();
        await Notes().InsertManyAsync(rows);
        return await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).ToListAsync();
    }

    protected static IReadOnlyList<Expression<Func<BehaveNote, object?>>> Cols(params Expression<Func<BehaveNote, object?>>[] columns) => columns;

    private async Task ForCapabilityAsync(Capability capability, Func<Task> behaviour, Func<OrmContext, Task> refusal)
    {
        Fixture.RequireAvailable();
        if (Ctx.Provider.Capabilities.Of(capability) != Support.None)
        {
            await behaviour();
            return;
        }
        await refusal(new OrmContext(new NeverUsedDataSource(), Ctx.Provider));
    }

    private async Task RefusedAsync(Capability capability, Func<Task> operation)
    {
        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(operation);
        Assert.Equal(capability, refusal.Capability);
        Assert.Equal(Ctx.Provider.Id, refusal.Provider);
        Assert.Contains($"'{Ctx.Provider.Id}'", refusal.Message, StringComparison.Ordinal);
    }

    [SkippableFact(DisplayName = "a row inserted through the library reads back with every value type intact")]
    public async Task Insert_and_read_round_trip()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var stamped = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234560);
        await Notes().InsertManyAsync([
            NoteRow(tenant, "Ünïcode ключ", 7, 1234.5678m, id).With(n => n.StampedAt, stamped),
        ]);

        var read = Assert.Single(await Notes().Where(n => n.TenantId == tenant).ToListAsync());

        Assert.Equal(id, read.Id);
        Assert.Equal(tenant, read.TenantId);
        Assert.Equal("Ünïcode ключ", read.Title);
        Assert.Equal(7, read.Stars);
        Assert.Equal(1234.5678m, read.Price);
        Assert.Equal(stamped, read.StampedAt!.Value.ToUniversalTime());
        Assert.Null(read.DeletedAt);
        Assert.Null(read.Doc);
    }

    [SkippableFact(DisplayName = "a lookup by id and by tenant finds the row whatever case the driver spells a guid in")]
    public async Task Guid_lookups_match()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var notes = await SeedAsync(tenant, ("one", 1), ("two", 2));
        var id1 = notes[1].Id;

        var byId = Assert.Single(await Notes().Where(n => n.Id == id1).ToListAsync());

        Assert.Equal("two", byId.Title);
        Assert.Equal(2, await Notes().Where(n => n.TenantId == tenant).CountAsync());
    }

    [SkippableFact(DisplayName = "where, order, take, skip, count, sum and any agree with the rows that were stored")]
    public async Task Reads_filter_order_and_page()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        await SeedAsync(tenant, ("a", 5), ("b", 3), ("c", 9), ("d", 1), ("e", 7));

        var top = await Notes().Where(n => n.TenantId == tenant && n.Stars >= 5).OrderByDescending(n => n.Stars).ToListAsync();
        var page = await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).Skip(1).Take(2).ToListAsync();
        var tail = await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).Skip(3).ToListAsync();

        Assert.Equal(["c", "e", "a"], top.Select(n => n.Title));
        Assert.Equal(["b", "c"], page.Select(n => n.Title));
        Assert.Equal(["d", "e"], tail.Select(n => n.Title));
        Assert.Equal(5, await Notes().Where(n => n.TenantId == tenant).CountAsync());
        Assert.Equal(25, await Notes().Where(n => n.TenantId == tenant).SumAsync(n => n.Stars));
        Assert.True(await Notes().Where(n => n.TenantId == tenant && n.Stars == 9).AnyAsync());
        Assert.False(await Notes().Where(n => n.TenantId == tenant && n.Stars == 100).AnyAsync());
    }

    [SkippableFact(DisplayName = "like matching is case sensitive and the ignore-case forms match any case, as in PostgreSQL")]
    public async Task Like_is_case_sensitive_and_ignore_case_is_not()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        await SeedAsync(tenant, ("Alpha", 1), ("alpha", 2), ("beta_x", 3), ("betaZx", 4), ("100%", 5));

        var exact = await Notes().Where(n => n.TenantId == tenant && n.Title.StartsWith("al")).ToListAsync();
        var folded = await Notes().Where(n => n.TenantId == tenant && n.Title.StartsWith("al", StringComparison.OrdinalIgnoreCase)).ToListAsync();
        var underscore = await Notes().Where(n => n.TenantId == tenant && n.Title.Contains("a_x")).ToListAsync();
        var percent = await Notes().Where(n => n.TenantId == tenant && n.Title.Contains("0%")).ToListAsync();
        var equalsFolded = await Notes().Where(n => n.TenantId == tenant && Db.EqualsIgnoreCase(n.Title, "ALPHA")).ToListAsync();

        Assert.Equal(["alpha"], exact.Select(n => n.Title));
        Assert.Equal(["Alpha", "alpha"], folded.Select(n => n.Title).Order(StringComparer.Ordinal));
        Assert.Equal(["beta_x"], underscore.Select(n => n.Title));
        Assert.Equal(["100%"], percent.Select(n => n.Title));
        Assert.Equal(2, equalsFolded.Count);
    }

    [SkippableFact(DisplayName = "an update changes the rows it names, a coalesce keeps an existing value, an increment adds and the clock is the database's")]
    public async Task Update_sets_increments_and_stamps()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var notes = await SeedAsync(tenant, ("u1", 1), ("u2", 2));
        var id0 = notes[0].Id;
        var id1 = notes[1].Id;
        var before = DateTime.UtcNow;

        var changed = await Notes().Where(n => n.Id == id0).Set(n => n.Title, "renamed").SetIncrement(n => n.Stars, 10).SetNow(n => n.StampedAt).UpdateAsync();
        var kept = await Notes().Where(n => n.Id == id1).SetCoalesce(n => n.Title, null).UpdateAsync();

        var first = Assert.Single(await Notes().Where(n => n.Id == id0).ToListAsync());
        var second = Assert.Single(await Notes().Where(n => n.Id == id1).ToListAsync());
        Assert.Equal(1, changed);
        Assert.Equal(1, kept);
        Assert.Equal("renamed", first.Title);
        Assert.Equal(11, first.Stars);
        Assert.InRange(first.StampedAt!.Value.ToUniversalTime(), before.AddSeconds(-30), DateTime.UtcNow.AddSeconds(30));
        Assert.Equal("u2", second.Title);
    }

    [SkippableFact(DisplayName = "a delete is soft: the row leaves live reads, stays in the trash, and a restore brings it back")]
    public async Task Soft_delete_and_restore()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var notes = await SeedAsync(tenant, ("keep", 1), ("trash", 2));
        var id1 = notes[1].Id;

        var deleted = await Notes().Where(n => n.Id == id1).DeleteAsync();

        Assert.Equal(1, deleted);
        Assert.Equal(["keep"], (await Notes().Where(n => n.TenantId == tenant).ToListAsync()).Select(n => n.Title));
        var trashed = Assert.Single(await Notes().IncludingDeleted().Where(n => n.TenantId == tenant && n.Title == "trash").ToListAsync());
        Assert.NotNull(trashed.DeletedAt);

        var restored = await Notes().RestoreAsync([id1], tenant);

        Assert.Equal(1, restored);
        Assert.Equal(2, await Notes().Where(n => n.TenantId == tenant).CountAsync());
        Assert.Equal(0, await Notes().RestoreAsync([id1], tenant));
    }

    [SkippableFact(DisplayName = "a purge removes the row for good, including a soft-deleted one, and only inside the tenant")]
    public async Task Purge_removes_for_good()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var notes = await SeedAsync(tenant, ("p1", 1), ("p2", 2));
        var id0 = notes[0].Id;
        var id1 = notes[1].Id;
        await Notes().Where(n => n.Id == id0).DeleteAsync();

        var foreign = await Notes().PurgeAsync([id0], Guid.NewGuid());
        var purged = await Notes().PurgeAsync([id0, id1], tenant);

        Assert.Equal(0, foreign);
        Assert.Equal(2, purged);
        Assert.Equal(0, await Notes().IncludingDeleted().Where(n => n.TenantId == tenant).CountAsync());
    }

    [SkippableFact(DisplayName = "the live-only unique index blocks a live duplicate, allows a title again after a soft delete, and refuses a restore into a live twin")]
    public async Task Live_only_index_follows_soft_delete()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var first = (await SeedAsync(tenant, ("dup", 1)))[0];

        await Assert.ThrowsAnyAsync<DbException>(() => Notes().InsertManyAsync([NoteRow(tenant, "dup")]));
        await Notes().Where(n => n.Id == first.Id).DeleteAsync();
        await Notes().InsertManyAsync([NoteRow(tenant, "dup", 2)]);

        Assert.Equal(1, await Notes().Where(n => n.TenantId == tenant).CountAsync());
        Assert.Equal(2, await Notes().IncludingDeleted().Where(n => n.TenantId == tenant).CountAsync());
        await Assert.ThrowsAnyAsync<DbException>(() => Notes().RestoreAsync([first.Id], tenant));
    }

    [SkippableFact(DisplayName = "insert-many splits across statements at the provider's bind limit and stores every row exactly once")]
    public async Task Insert_many_chunks_past_the_bind_limit()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var width = 5;
        var count = Ctx.Provider.MaxBindParameters / width + 1000;
        var rows = Enumerable.Range(0, count).Select(i => NoteRow(tenant, "bulk" + i)).ToList();

        var affected = await Notes().InsertManyAsync(rows);

        Assert.Equal(count, affected);
        Assert.Equal(count, await Notes().Where(n => n.TenantId == tenant).CountAsync());
        Assert.Equal(count, (await Notes().Where(n => n.TenantId == tenant).ToListAsync()).Select(n => n.Title).Distinct().Count());
    }

    [SkippableFact(DisplayName = "a failing insert-many rolls every chunk back")]
    public async Task Insert_many_is_atomic()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var width = 5;
        var count = Ctx.Provider.MaxBindParameters / width + 50;
        var rows = Enumerable.Range(0, count).Select(i => NoteRow(tenant, "atomic" + i)).ToList();
        rows[^1] = NoteRow(tenant, "atomic0");

        await Assert.ThrowsAnyAsync<DbException>(() => Notes().InsertManyAsync(rows));

        Assert.Equal(0, await Notes().Where(n => n.TenantId == tenant).CountAsync());
    }

    [SkippableFact(DisplayName = "an upsert inserts, then updates the conflicting row and returns the stored columns, and do-nothing leaves the row alone")]
    public async Task Capability_Upsert()
    {
        await ForCapabilityAsync(
            Capability.Upsert,
            async () =>
            {
                var tenant = Guid.NewGuid();
                var id = Guid.NewGuid();
                var returning = Cols(n => n.Stars, n => n.Title);
                var sets = new[] { UpsertSet<BehaveNote>.Inserted(n => n.Stars), UpsertSet<BehaveNote>.Inserted(n => n.Title) };
                var key = Cols(n => n.Id);

                var inserted = await Notes().UpsertAsync(NoteRow(tenant, "first", 1, null, id), key, OnConflict.DoUpdate, sets, returning);
                var updated = await Notes().UpsertAsync(NoteRow(tenant, "second", 9, null, id), key, OnConflict.DoUpdate, sets, returning);
                var ignored = await Notes().UpsertAsync(NoteRow(tenant, "third", 99, null, id), key, OnConflict.DoNothing, null, returning);

                Assert.Equal(new object?[] { 1L, "first" }, Assert.Single(inserted).Select(Normal));
                Assert.Equal(new object?[] { 9L, "second" }, Assert.Single(updated).Select(Normal));
                Assert.Empty(ignored);
                var stored = Assert.Single(await Notes().Where(n => n.Id == id).ToListAsync());
                Assert.Equal(("second", 9), (stored.Title, stored.Stars));
            },
            ctx => RefusedAsync(Capability.Upsert, () => new Query<BehaveNote>(ctx, BehaveNote.Map).UpsertAsync(
                NoteRow(Guid.NewGuid(), "x"), Cols(n => n.Id), OnConflict.DoNothing)));
    }

    [SkippableFact(DisplayName = "an upsert never overwrites a soft-deleted row and says so, and targets the live rows only through the live-only index")]
    public async Task Upsert_respects_soft_deleted_rows()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();
        var first = (await SeedAsync(tenant, ("live", 1)))[0];
        await Notes().Where(n => n.Id == first.Id).DeleteAsync();
        var sets = new[] { UpsertSet<BehaveNote>.Inserted(n => n.Stars) };

        await Assert.ThrowsAsync<SoftDeletedConflictException>(() =>
            Notes().UpsertAsync(NoteRow(tenant, "live", 5, null, first.Id), Cols(n => n.Id), OnConflict.DoUpdate, sets));
        await Notes().UpsertAsync(NoteRow(tenant, "live", 6), Cols(n => n.TenantId, n => n.Title), OnConflict.DoUpdate, sets);

        Assert.Equal(1, await Notes().Where(n => n.TenantId == tenant).CountAsync());
        Assert.Equal(6, (await Notes().Where(n => n.TenantId == tenant).ToListAsync())[0].Stars);
    }

    [SkippableFact(DisplayName = "update and delete return the changed columns, or refuse before any statement runs")]
    public async Task Capability_Returning()
    {
        await ForCapabilityAsync(
            Capability.Returning,
            async () =>
            {
                var tenant = Guid.NewGuid();
                var notes = await SeedAsync(tenant, ("r1", 1), ("r2", 2));

                var updated = await Notes().Where(n => n.TenantId == tenant && n.Stars == 2).Set(n => n.Stars, 20).UpdateReturningAsync(Cols(n => n.Title, n => n.Stars));
                var deleted = await Notes().Where(n => n.TenantId == tenant && n.Stars == 1).DeleteReturningAsync(Cols(n => n.Id));

                Assert.Equal(new object?[] { "r2", 20L }, Assert.Single(updated).Select(Normal));
                var deletedId = Assert.Single(deleted)[0];
                Assert.Equal(notes[0].Id, deletedId is Guid guid ? guid : Guid.Parse((string)deletedId!));
            },
            ctx => RefusedAsync(Capability.Returning, () => new Query<BehaveNote>(ctx, BehaveNote.Map)
                .Where(n => n.Stars == 1).Set(n => n.Stars, 2).UpdateReturningAsync(Cols(n => n.Id))));
    }

    [SkippableFact(DisplayName = "a column matched against a list of values finds exactly those rows, and an empty list finds none")]
    public async Task Capability_ArrayParameter()
    {
        await ForCapabilityAsync(
            Capability.ArrayParameter,
            async () =>
            {
                var tenant = Guid.NewGuid();
                var notes = await SeedAsync(tenant, ("x1", 1), ("x2", 2), ("x3", 3));
                var ids = new[] { notes[0].Id, notes[2].Id, Guid.NewGuid() };
                var titles = new List<string> { "x2", "zzz" };
                var none = Array.Empty<Guid>();

                var byId = await Notes().Where(n => Db.AnyOf(n.Id, ids)).OrderBy(n => n.Title).ToListAsync();
                var byTitle = await Notes().Where(n => n.TenantId == tenant && Db.AnyOf(n.Title, titles)).ToListAsync();
                var byNothing = await Notes().Where(n => Db.AnyOf(n.Id, none)).ToListAsync();

                Assert.Equal(["x1", "x3"], byId.Select(n => n.Title));
                Assert.Equal(["x2"], byTitle.Select(n => n.Title));
                Assert.Empty(byNothing);
            },
            ctx => RefusedAsync(Capability.ArrayParameter, () => new Query<BehaveNote>(ctx, BehaveNote.Map)
                .Where(n => Db.AnyOf(n.Title, new[] { "a" })).ToListAsync()));
    }

    [SkippableFact(DisplayName = "null-safe equality matches null with null and a value with itself, or refuses before any statement runs")]
    public async Task Capability_NullSafeEquality()
    {
        await ForCapabilityAsync(
            Capability.NullSafeEquality,
            async () =>
            {
                var tenant = Guid.NewGuid();
                await Notes().InsertManyAsync([NoteRow(tenant, "n1", 1, null), NoteRow(tenant, "n2", 2, 5m)]);
                decimal? none = null;
                decimal? five = 5m;

                var nulls = await Notes().Where(n => n.TenantId == tenant && Db.NullSafeEquals(n.Price, none)).ToListAsync();
                var fives = await Notes().Where(n => n.TenantId == tenant && Db.NullSafeEquals(n.Price, five)).ToListAsync();

                Assert.Equal(["n1"], nulls.Select(n => n.Title));
                Assert.Equal(["n2"], fives.Select(n => n.Title));
            },
            ctx => RefusedAsync(Capability.NullSafeEquality, () => new Query<BehaveNote>(ctx, BehaveNote.Map)
                .Where(n => Db.NullSafeEquals(n.Stars, 1)).ToListAsync()));
    }

    [SkippableFact(DisplayName = "a counted aggregate with a filter and a plain count come back from one statement, or the filter is refused before any statement runs")]
    public async Task Capability_AggregateFilter()
    {
        await ForCapabilityAsync(
            Capability.AggregateFilter,
            async () =>
            {
                var tenant = Guid.NewGuid();
                await SeedAsync(tenant, ("f1", 1), ("f2", 5), ("f3", 9));

                var row = await Notes().Where(n => n.TenantId == tenant).AggregateCount().AggregateCountWhere(n => n.Stars >= 5).AggregateMin(n => n.Stars).AggregatesAsync();

                Assert.Equal(new object?[] { 3L, 2L, 1L }, row.Select(Normal));
            },
            ctx => RefusedAsync(Capability.AggregateFilter, () =>
            {
                new Query<BehaveNote>(ctx, BehaveNote.Map).AggregateCountWhere(n => n.Stars > 1);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a row lock inside a unit of work serialises read-modify-write, so concurrent increments lose nothing")]
    public async Task Capability_RowLock()
    {
        await ForCapabilityAsync(
            Capability.RowLock,
            () => NoLostUpdateAsync(static q => q.ForUpdate()),
            ctx => RefusedAsync(Capability.RowLock, () =>
            {
                new Query<BehaveNote>(ctx, BehaveNote.Map).ForUpdate();
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a key-preserving row lock serialises read-modify-write the same way, or is refused before any statement runs")]
    public async Task Capability_KeyPreservingRowLock()
    {
        await ForCapabilityAsync(
            Capability.KeyPreservingRowLock,
            () => NoLostUpdateAsync(static q => q.ForNoKeyUpdate()),
            ctx => RefusedAsync(Capability.KeyPreservingRowLock, () =>
            {
                new Query<BehaveNote>(ctx, BehaveNote.Map).ForNoKeyUpdate();
                return Task.CompletedTask;
            }));
    }

    private async Task NoLostUpdateAsync(Func<Query<BehaveCounter>, Query<BehaveCounter>> locked)
    {
        var id = Guid.NewGuid();
        await Counters().InsertManyAsync([new Row<BehaveCounter>().With(c => c.Id, id).With(c => c.N, 0)]);
        const int workers = 6;
        const int each = 15;

        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < each; i++)
            {
                await using var work = await UnitOfWork.BeginAsync(Ctx);
                var current = await locked(Counters(work).Where(c => c.Id == id)).FirstOrDefaultAsync();
                await Counters(work).Where(c => c.Id == id).Set(c => c.N, current!.N + 1).UpdateAsync();
                await work.CommitAsync();
            }
        })));

        Assert.Equal(workers * each, (await Counters().Where(c => c.Id == id).ToListAsync())[0].N);
    }

    [SkippableFact(DisplayName = "two writers in different lanes each commit every unit of work and lose no update, with no busy failure")]
    public async Task Lanes_with_two_concurrent_writers()
    {
        Fixture.RequireAvailable();
        var id = Guid.NewGuid();
        await Counters().InsertManyAsync([new Row<BehaveCounter>().With(c => c.Id, id).With(c => c.N, 0)]);
        const int each = 40;
        var committed = new int[2];

        async Task WriterAsync(DbLaneKind lane, int slot)
        {
            using var scope = DbLane.Enter(lane);
            for (var i = 0; i < each; i++)
            {
                await using var work = await UnitOfWork.BeginAsync(Ctx);
                var current = await Counters(work).Where(c => c.Id == id).ForUpdate().FirstOrDefaultAsync();
                await Counters(work).Where(c => c.Id == id).Set(c => c.N, current!.N + 1).UpdateAsync();
                await work.CommitAsync();
                committed[slot]++;
            }
        }

        await Task.WhenAll(Task.Run(() => WriterAsync(DbLaneKind.Request, 0)), Task.Run(() => WriterAsync(DbLaneKind.Background, 1)));

        Assert.Equal([each, each], committed);
        Assert.Equal(2 * each, (await Counters().Where(c => c.Id == id).ToListAsync())[0].N);
    }

    [SkippableFact(DisplayName = "standalone writes from two lanes at once all land, and a read never waits for them")]
    public async Task Standalone_writes_from_two_lanes()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();

        async Task WriterAsync(DbLaneKind lane, string prefix)
        {
            using var scope = DbLane.Enter(lane);
            for (var i = 0; i < 30; i++) await Notes().InsertManyAsync([NoteRow(tenant, prefix + i)]);
        }

        var reading = Task.Run(async () =>
        {
            using var scope = DbLane.Enter(DbLaneKind.Request);
            for (var i = 0; i < 30; i++) await Notes().Where(n => n.TenantId == tenant).CountAsync();
        });
        await Task.WhenAll(
            Task.Run(() => WriterAsync(DbLaneKind.Request, "a")),
            Task.Run(() => WriterAsync(DbLaneKind.Background, "b")),
            Task.Run(() => WriterAsync(DbLaneKind.Ops, "c")),
            reading);

        Assert.Equal(90, await Notes().Where(n => n.TenantId == tenant).CountAsync());
    }

    // The second unit of work starts only once the first holds the lock (a signal, not a delay), and the first
    // commits only after the second has begun to ask for it, so the order does not depend on how fast a runner is.
    // "Begun to ask" is signalled BEFORE the second unit of work begins, because where the exclusion sits depends on
    // the provider: PostgreSQL admits the second unit of work and holds it in LockAsync, SQLite holds it already in
    // BeginAsync (the single-writer gate is taken for the whole unit of work and the emulated lock adds nothing), so a
    // signal given after BeginAsync would never come while the first waits for it.
    // The pause before the commit gives a lock that does not exclude the chance to show it; it asserts nothing.
    private async Task<List<string>> ExclusiveLockOrderAsync()
    {
        var key = "lock-" + Guid.NewGuid();
        var committed = false;
        var order = new List<string>();
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await using var work = await UnitOfWork.BeginAsync(Ctx);
            await work.LockAsync(key);
            lock (order) order.Add("first holds");
            holding.TrySetResult();
            await Task.WhenAny(asking.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            await Task.Delay(400);
            lock (order) order.Add("first commits");
            committed = true;
            await work.CommitAsync();
        });
        await Task.WhenAny(holding.Task, first);
        var second = Task.Run(async () =>
        {
            asking.TrySetResult();
            await using var work = await UnitOfWork.BeginAsync(Ctx);
            await work.LockAsync(key);
            lock (order) order.Add(committed ? "second after commit" : "second overlapped");
            await work.CommitAsync();
        });
        await Task.WhenAll(first, second);
        return order;
    }

    [SkippableFact(DisplayName = "an exclusive named lock keeps a second unit of work out until the first commits, and a shared one admits shared holders only")]
    public async Task Capability_NamedLock()
    {
        await ForCapabilityAsync(
            Capability.NamedLock,
            async () =>
            {
                Assert.Equal(["first holds", "first commits", "second after commit"], await ExclusiveLockOrderAsync());

                var sharedKey = "shared-" + Guid.NewGuid();
                async Task SharedAsync()
                {
                    await using var work = await UnitOfWork.BeginAsync(Ctx);
                    await work.LockSharedAsync(sharedKey);
                    await Task.Delay(50);
                    await work.CommitAsync();
                }

                await Task.WhenAll(SharedAsync(), SharedAsync(), SharedAsync());
            },
            async _ =>
            {
                await using var work = await UnitOfWork.BeginAsync(Ctx);
                await RefusedAsync(Capability.NamedLock, () => work.LockAsync("k"));
                await RefusedAsync(Capability.NamedLock, () => work.LockSharedAsync("k"));
            });
    }

    [SkippableFact(DisplayName = "a session lease is held by one caller at a time and released on disposal, or refused before any connection is opened")]
    public async Task Capability_SessionLock()
    {
        await ForCapabilityAsync(
            Capability.SessionLock,
            async () =>
            {
                var key = Random.Shared.NextInt64(1, long.MaxValue);

                var held = await AdvisorySessionLocks.TryLeaseAsync(Ctx.Provider, Ctx.DataSource, key, CancellationToken.None);
                var second = await AdvisorySessionLocks.TryLeaseAsync(Ctx.Provider, Ctx.DataSource, key, CancellationToken.None);

                Assert.NotNull(held);
                Assert.Null(second);
                await held!.DisposeAsync();
                await using var again = await AdvisorySessionLocks.TryLeaseAsync(Ctx.Provider, Ctx.DataSource, key, CancellationToken.None);
                Assert.NotNull(again);
            },
            _ => RefusedAsync(Capability.SessionLock, () => AdvisorySessionLocks.TryLeaseAsync(Ctx.Provider, Ctx.DataSource, 1, CancellationToken.None)));
    }

    [SkippableFact(DisplayName = "adding minutes to the database clock lands that far ahead, or is refused before any statement runs")]
    public async Task Capability_IntervalArithmetic()
    {
        await ForCapabilityAsync(
            Capability.IntervalArithmetic,
            async () =>
            {
                var tenant = Guid.NewGuid();
                var note = (await SeedAsync(tenant, ("lease", 1)))[0];
                var before = DateTime.UtcNow;

                await Notes().Where(n => n.Id == note.Id).SetNowPlusMinutes(n => n.StampedAt, 30).UpdateAsync();
                var ahead = (await Notes().Where(n => n.Id == note.Id).ToListAsync())[0].StampedAt!.Value.ToUniversalTime();
                await Notes().Where(n => n.Id == note.Id).SetNowPlusMinutes(n => n.StampedAt, -30).UpdateAsync();
                var behind = (await Notes().Where(n => n.Id == note.Id).ToListAsync())[0].StampedAt!.Value.ToUniversalTime();

                Assert.InRange(ahead, before.AddMinutes(29.5), DateTime.UtcNow.AddMinutes(30.5));
                Assert.InRange(behind, before.AddMinutes(-30.5), DateTime.UtcNow.AddMinutes(-29.5));
            },
            ctx => RefusedAsync(Capability.IntervalArithmetic, () =>
            {
                new Query<BehaveNote>(ctx, BehaveNote.Map).SetNowPlusMinutes(n => n.StampedAt, 5);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a JSON document column is filtered by key, or refused before any statement runs")]
    public async Task Capability_JsonDocument()
    {
        await ForCapabilityAsync(
            Capability.JsonDocument,
            async () =>
            {
                var tenant = Guid.NewGuid();
                await Notes().InsertManyAsync([
                    NoteRow(tenant, "j1").With(n => n.Doc, Jsonb.Parse("{\"kind\":\"red\"}")),
                    NoteRow(tenant, "j2").With(n => n.Doc, Jsonb.Parse("{\"other\":1}")),
                ]);

                var withKey = await Notes().Where(n => n.TenantId == tenant && Db.JsonHasKey(n.Doc, "kind")).ToListAsync();

                Assert.Equal(["j1"], withKey.Select(n => n.Title));
            },
            ctx => RefusedAsync(Capability.JsonDocument, () => new Query<BehaveNote>(ctx, BehaveNote.Map)
                .Where(n => Db.JsonHasKey(n.Doc, "kind")).ToListAsync()));
    }

    [SkippableFact(DisplayName = "a unit of work that is not committed leaves nothing behind, and one that commits keeps its rows")]
    public async Task Unit_of_work_commits_or_rolls_back()
    {
        Fixture.RequireAvailable();
        var tenant = Guid.NewGuid();

        await using (var abandoned = await UnitOfWork.BeginAsync(Ctx))
            await Notes(abandoned).InsertManyAsync([NoteRow(tenant, "gone")]);
        await using (var kept = await UnitOfWork.BeginAsync(Ctx))
        {
            await Notes(kept).InsertManyAsync([NoteRow(tenant, "kept")]);
            await kept.CommitAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(kept.CommitAsync);
        }

        Assert.Equal(["kept"], (await Notes().Where(n => n.TenantId == tenant).ToListAsync()).Select(n => n.Title));
    }

    [SkippableFact(DisplayName = "the context answers a ping")]
    public async Task Ping_answers()
    {
        Fixture.RequireAvailable();

        Assert.True(await Ctx.PingAsync());
    }

    private Task ApplyAsync(Node operation) => MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [operation]);

    [SkippableFact(DisplayName = "a partial unique index admits duplicates outside its predicate and refuses them inside it, or is refused before any statement runs")]
    public async Task Capability_PartialIndex()
    {
        var index = new CreateIndex(
            "behave_part_uq", "behave_part", [new ColumnRef("k")], true,
            new Where([new Compare(CompareOp.Greater, new ColumnRef("k"), new IntLit(0))]));
        await ForCapabilityAsync(
            Capability.PartialIndex,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_part (id TEXT PRIMARY KEY, k INTEGER NOT NULL)");
                await ApplyAsync(index);

                await Fixture.ExecuteAsync("INSERT INTO behave_part VALUES ('a', 5)");
                await Fixture.ExecuteAsync("INSERT INTO behave_part VALUES ('c', -1)");
                await Fixture.ExecuteAsync("INSERT INTO behave_part VALUES ('d', -1)");

                await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync("INSERT INTO behave_part VALUES ('b', 5)"));
            },
            _ => RefusedAsync(Capability.PartialIndex, () =>
            {
                DdlCompiler.Compile(index, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    private async Task<string> StoredIndexAsync(string table, string index)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return (await Ctx.Provider.CreateCatalog(connection).IndexesAsync(table)).Single(i => i.Name == index).Definition;
    }

    [SkippableFact(DisplayName = "an index term that states where nulls sort keeps that placement in the stored definition, or is refused by name before any statement runs")]
    public async Task Capability_IndexNullsOrdering()
    {
        var index = new CreateIndex("behave_nulls_ix", "behave_nulls", [new OrderTerm(new ColumnRef("k"), true, NullsPlacement.Last)]);
        await ForCapabilityAsync(
            Capability.IndexNullsOrdering,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_nulls (id TEXT PRIMARY KEY, k INTEGER)");
                await ApplyAsync(index);

                Assert.Contains("DESC NULLS LAST", await StoredIndexAsync("behave_nulls", "behave_nulls_ix"), StringComparison.Ordinal);
            },
            _ => RefusedAsync(Capability.IndexNullsOrdering, () =>
            {
                DdlCompiler.Compile(index, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "an index term with a collation keeps it in the stored definition, or is refused before any statement runs")]
    public async Task Capability_IndexCollation()
    {
        Fixture.RequireAvailable();
        var collation = Ctx.Provider.Id == "sqlite" ? "NOCASE" : "C";
        var index = new CreateIndex("behave_coll_ix", "behave_coll", [new OrderTerm(new ColumnRef("k"), false, NullsPlacement.Unspecified, collation)], true);
        await ForCapabilityAsync(
            Capability.IndexCollation,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_coll (id TEXT PRIMARY KEY, k TEXT)");
                await ApplyAsync(index);

                Assert.Contains("COLLATE", await StoredIndexAsync("behave_coll", "behave_coll_ix"), StringComparison.OrdinalIgnoreCase);
                await Fixture.ExecuteAsync("INSERT INTO behave_coll VALUES ('a', 'x')");
                if (collation == "NOCASE")
                    await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync("INSERT INTO behave_coll VALUES ('b', 'X')"));
            },
            _ => RefusedAsync(Capability.IndexCollation, () =>
            {
                DdlCompiler.Compile(index, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a unique index over lower(column) refuses two values that differ only in case, or is refused before any statement runs")]
    public async Task Capability_IndexExpression()
    {
        var index = new CreateIndex("behave_expr_ix", "behave_expr", [new Call(Fn.Lower, [new ColumnRef("k")])], true);
        await ForCapabilityAsync(
            Capability.IndexExpression,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_expr (id TEXT PRIMARY KEY, k TEXT)");
                await ApplyAsync(index);

                await Fixture.ExecuteAsync("INSERT INTO behave_expr VALUES ('a', 'x')");
                await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync("INSERT INTO behave_expr VALUES ('b', 'X')"));
            },
            _ => RefusedAsync(Capability.IndexExpression, () =>
            {
                DdlCompiler.Compile(index, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a foreign key can be dropped from an existing table, or the change is refused before any statement runs")]
    public async Task Capability_AlterConstraint()
    {
        var drop = new DropForeignKey("fk_behave_child_parent", "behave_child");
        await ForCapabilityAsync(
            Capability.AlterConstraint,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_parent (id TEXT PRIMARY KEY)");
                await Fixture.ExecuteAsync("CREATE TABLE behave_child (id TEXT PRIMARY KEY, pid TEXT, CONSTRAINT fk_behave_child_parent FOREIGN KEY (pid) REFERENCES behave_parent (id))");
                await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync("INSERT INTO behave_child VALUES ('c1', 'missing')"));

                await ApplyAsync(drop);

                await Fixture.ExecuteAsync("INSERT INTO behave_child VALUES ('c2', 'missing')");
            },
            _ => RefusedAsync(Capability.AlterConstraint, () =>
            {
                DdlCompiler.Compile(drop, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "the soft-delete column with its marker can be added to a table, or is refused before any statement runs")]
    public async Task Capability_ObjectMarker()
    {
        var marker = new SoftDeleteColumn("behave_mark", "v1", false);
        await ForCapabilityAsync(
            Capability.ObjectMarker,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_mark (id TEXT PRIMARY KEY)");

                await ApplyAsync(marker);

                Assert.Empty(await Fixture.RowsAsync("SELECT deleted_at FROM behave_mark"));
            },
            _ => RefusedAsync(Capability.ObjectMarker, () =>
            {
                DdlCompiler.Compile(marker, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a reconcile set creates an index where its table and column exist and skips one whose table is missing without failing, or is refused before any statement runs")]
    public async Task Capability_CatalogGuard()
    {
        static ReconcileIndex Item(string index, string table) => new(
            new CreateIndex(index, table, NodeList.Of([new ColumnRef("k")]), true, null),
            NodeList.Of([new ColumnRef("k")]));
        var present = new ReconcileIndexes([Item("behave_cat_uq", "behave_cat"), Item("behave_gone_uq", "behave_gone")]);
        await ForCapabilityAsync(
            Capability.CatalogGuard,
            async () =>
            {
                await Fixture.ExecuteAsync("CREATE TABLE behave_cat (id TEXT PRIMARY KEY, k INTEGER NOT NULL)");

                await ApplyAsync(present);

                await Fixture.ExecuteAsync("INSERT INTO behave_cat VALUES ('a', 5)");
                await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync("INSERT INTO behave_cat VALUES ('b', 5)"));
            },
            _ => RefusedAsync(Capability.CatalogGuard, () =>
            {
                DdlCompiler.Compile(present, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "the row-history capture can be installed and is there afterwards: the capture function on PostgreSQL, the connection functions on SQLite, or the install is refused before any statement runs")]
    public async Task Capability_RowHistoryTriggers()
    {
        var function = new RowHistoryFunction(OrmLiteSettings.DefaultActorSetting);
        await ForCapabilityAsync(
            Capability.RowHistoryTriggers,
            async () =>
            {
                await ApplyAsync(function);

                var present = Ctx.Provider.Id == "sqlite"
                    ? "SELECT count(DISTINCT name) FROM pragma_function_list WHERE name IN ('orm_actor', 'orm_tx', 'orm_tx_at')"
                    : "SELECT count(*) FROM pg_proc WHERE proname = 'row_history_capture'";
                Assert.Equal(Ctx.Provider.Id == "sqlite" ? 3L : 1L, (await Fixture.RowsAsync(present))[0][0]);
            },
            _ => RefusedAsync(Capability.RowHistoryTriggers, () =>
            {
                DdlCompiler.Compile(function, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a decimal column stores a value no double holds exactly and refuses a second row with the same value written with another scale, or is refused before any statement runs")]
    public async Task Capability_ExactDecimal()
    {
        var table = new CreateTable("behave_exact", [new ColumnDef("v", new TypeRef(ColumnType.Decimal, 28, 7), false, true)]);
        await ForCapabilityAsync(
            Capability.ExactDecimal,
            async () =>
            {
                await ApplyAsync(table);
                await Fixture.ExecuteAsync("INSERT INTO behave_exact VALUES ('12345678901234567890.1234567')");

                var read = (await Fixture.RowsAsync("SELECT v FROM behave_exact"))[0][0];

                Assert.Equal(12345678901234567890.1234567m, decimal.Parse(Convert.ToString(read, System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.CultureInfo.InvariantCulture));
                await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync("INSERT INTO behave_exact VALUES ('12345678901234567890.12345670')"));
            },
            _ => RefusedAsync(Capability.ExactDecimal, () =>
            {
                DdlCompiler.Compile(table, Ctx.Provider);
                return Task.CompletedTask;
            }));
    }

    [SkippableFact(DisplayName = "a truncating WAL checkpoint of the shared database completes and leaves an empty log, or is refused before any connection opens")]
    public async Task Capability_WalCheckpoint()
    {
        await ForCapabilityAsync(
            Capability.WalCheckpoint,
            async () =>
            {
                await Fixture.ExecuteAsync("INSERT INTO behave_counters VALUES ('wal-" + Guid.NewGuid().ToString("N") + "', 1)");

                var result = await Ctx.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));

                Assert.Equal(MaintenanceStatus.Completed, result.Status);
                Assert.Equal(result.WalFrames, result.CheckpointedFrames);
            },
            ctx => RefusedAsync(Capability.WalCheckpoint,
                () => ctx.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive))));
    }

    [SkippableFact(DisplayName = "a plain VACUUM of the whole shared database completes, or is refused before any connection opens")]
    public async Task Capability_Vacuum()
    {
        await ForCapabilityAsync(
            Capability.Vacuum,
            async () =>
            {
                var result = await Ctx.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain));

                Assert.Equal(MaintenanceStatus.Completed, result.Status);
                Assert.Equal(MaintenanceOperation.Vacuum, result.Operation);
            },
            ctx => RefusedAsync(Capability.Vacuum, () => ctx.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain))));
    }

    [SkippableFact(DisplayName = "VACUUM (FULL) of one table keeps its rows, or is refused by name before any connection opens")]
    public async Task Capability_VacuumFull()
    {
        var counters = EntityDescriptor.Of("BehaveCounter", "behave_counters", true);
        await ForCapabilityAsync(
            Capability.VacuumFull,
            async () =>
            {
                var id = Guid.NewGuid().ToString();
                await Fixture.ExecuteAsync("INSERT INTO behave_counters VALUES ('" + id + "', 7)");

                var result = await Ctx.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = counters });

                Assert.Equal(MaintenanceStatus.Completed, result.Status);
                Assert.Equal(7L, Convert.ToInt64((await Fixture.RowsAsync("SELECT n FROM behave_counters WHERE id = '" + id + "'"))[0][0], System.Globalization.CultureInfo.InvariantCulture));
            },
            ctx => RefusedAsync(Capability.VacuumFull, () => ctx.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full))));
    }

    [SkippableFact(DisplayName = "a plain VACUUM of one table completes, or is refused by name before any connection opens")]
    public async Task Capability_VacuumTable()
    {
        var counters = EntityDescriptor.Of("BehaveCounter", "behave_counters", true);
        await ForCapabilityAsync(
            Capability.VacuumTable,
            async () =>
            {
                var result = await Ctx.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Table = counters });

                Assert.Equal(MaintenanceStatus.Completed, result.Status);
                Assert.Equal("behave_counters", result.Table);
            },
            ctx => RefusedAsync(Capability.VacuumTable,
                () => ctx.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Table = counters })));
    }

    [SkippableFact(DisplayName = "ANALYZE of one table and of the whole shared database completes and changes no row, or is refused before any connection opens")]
    public async Task Capability_Analyze()
    {
        var counters = EntityDescriptor.Of("BehaveCounter", "behave_counters", true);
        await ForCapabilityAsync(
            Capability.Analyze,
            async () =>
            {
                var before = await Fixture.RowsAsync("SELECT id, n FROM behave_counters ORDER BY id");

                var table = await Ctx.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Table) { Table = counters });
                var database = await Ctx.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database));

                Assert.Equal(MaintenanceStatus.Completed, table.Status);
                Assert.Equal(MaintenanceStatus.Completed, database.Status);
                Assert.Equal(before, await Fixture.RowsAsync("SELECT id, n FROM behave_counters ORDER BY id"));
            },
            ctx => RefusedAsync(Capability.Analyze, () => ctx.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database))));
    }

    private static object? Normal(object? value) => value switch
    {
        long number => number,
        int number => (long)number,
        _ => value,
    };
}
