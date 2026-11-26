using System.Linq.Expressions;
using Neemle.OrmLite.Tests.Streaming;
using Neemle.OrmLite.Tests.Behaviour;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

public static class TerminalCensus
{
    internal sealed record CensusEntry(
        string Name,
        string[] Members,
        bool PostgresOnly,
        bool Paged,
        Func<Query<BehaveNote>, Task> Run);

    private static readonly Guid Probe = Guid.Parse("90000000-0000-0000-0000-000000000009");

    private static readonly IReadOnlyList<Expression<Func<BehaveNote, object?>>> Id = [n => n.Id];

    private static Row<BehaveNote> NewRow() => new Row<BehaveNote>()
        .With(n => n.Id, Guid.NewGuid())
        .With(n => n.TenantId, Guid.NewGuid())
        .With(n => n.Title, "census")
        .With(n => n.Stars, 1);

    private static Func<Query<BehaveNote>, Task> Sync(Action<Query<BehaveNote>> run) => q =>
    {
        run(q);
        return Task.CompletedTask;
    };

    internal static readonly IReadOnlyList<CensusEntry> Entries =
    [
        new("ToListAsync",
            [nameof(Query<BehaveNote>.ToListAsync)],
            false, true,
            q => q.ToListAsync()),
        new("StreamAsync", [nameof(Query<BehaveNote>.StreamAsync)], false, true,
            q => StreamingProviderChecks.CollectAsync(q.StreamAsync())),
        new("StreamProjectAsync", [nameof(Query<BehaveNote>.StreamProjectAsync)], false, true,
            q => StreamingProviderChecks.CollectAsync(q.StreamProjectAsync(Id, r => r.GetGuid(0)))),
        new("StreamProjectionAsync", [nameof(Query<BehaveNote>.StreamProjectionAsync)], false, true,
            q => StreamingProviderChecks.CollectAsync(q.StreamProjectionAsync(r => r.GetGuid(0)))),
        new("StreamColumnAsync", [nameof(Query<BehaveNote>.StreamColumnAsync)], false, true,
            q => StreamingProviderChecks.CollectAsync(q.StreamColumnAsync<Guid>(n => n.Id))),
        new("CompileSelect and RenderSelect",
            [
                nameof(Query<BehaveNote>.CompileSelect),
                nameof(Query<BehaveNote>.RenderSelect),
            ],
            false, true,
            Sync(q => { q.CompileSelect(); q.RenderSelect(); })),
        new("ProjectAsync",
            [nameof(Query<BehaveNote>.ProjectAsync)],
            false, true,
            q => q.ProjectAsync(Id, r => r.GetGuid(0))),
        new("CompileProject and RenderProject",
            [
                nameof(Query<BehaveNote>.CompileProject),
                nameof(Query<BehaveNote>.RenderProject),
            ],
            false, true,
            Sync(q => { q.CompileProject(Id); q.RenderProject(Id); })),
        new("ProjectionAsync",
            [nameof(Query<BehaveNote>.ProjectionAsync)],
            false, true,
            q => q.ProjectionAsync(r => r.GetGuid(0))),
        new("CompileProjection and RenderProjection",
            [
                nameof(Query<BehaveNote>.CompileProjection),
                nameof(Query<BehaveNote>.RenderProjection),
            ],
            false, true,
            Sync(q => { q.CompileProjection(); q.RenderProjection(); })),
        new("FirstOrDefaultAsync",
            [nameof(Query<BehaveNote>.FirstOrDefaultAsync)],
            false, true,
            q => q.FirstOrDefaultAsync()),
        new("ScalarAsync",
            [nameof(Query<BehaveNote>.ScalarAsync)],
            false, true,
            q => q.ScalarAsync<string>(n => n.Title)),
        new("CompileScalar and RenderScalar",
            [
                nameof(Query<BehaveNote>.CompileScalar),
                nameof(Query<BehaveNote>.RenderScalar),
            ],
            false, true,
            Sync(q => { q.CompileScalar(n => n.Title); q.RenderScalar(n => n.Title); })),
        new("CountAsync",
            [nameof(Query<BehaveNote>.CountAsync)],
            false, false,
            q => q.CountAsync()),
        new("SumAsync",
            [nameof(Query<BehaveNote>.SumAsync)],
            false, false,
            q => q.SumAsync(n => n.Stars)),
        new("AnyAsync",
            [nameof(Query<BehaveNote>.AnyAsync)],
            false, false,
            q => q.AnyAsync()),
        new("AggregatesAsync",
            [nameof(Query<BehaveNote>.AggregatesAsync)],
            false, false,
            q => q.AggregatesAsync()),
        new("CompileAggregates and RenderAggregates",
            [
                nameof(Query<BehaveNote>.CompileAggregates),
                nameof(Query<BehaveNote>.RenderAggregates),
            ],
            false, false,
            Sync(q => { q.CompileAggregates(); q.RenderAggregates(); })),
        new("UpdateAsync",
            [nameof(Query<BehaveNote>.UpdateAsync)],
            false, false,
            q => q.UpdateAsync()),
        new("CompileUpdate and RenderUpdate",
            [
                nameof(Query<BehaveNote>.CompileUpdate),
                nameof(Query<BehaveNote>.RenderUpdate),
            ],
            false, false,
            Sync(q => { q.CompileUpdate(); q.RenderUpdate(); })),
        new("DeleteAsync",
            [nameof(Query<BehaveNote>.DeleteAsync)],
            false, false,
            q => q.DeleteAsync()),
        new("CompileDelete and RenderDelete",
            [
                nameof(Query<BehaveNote>.CompileDelete),
                nameof(Query<BehaveNote>.RenderDelete),
            ],
            false, false,
            Sync(q => { q.CompileDelete(); q.RenderDelete(); })),
        new("PurgeAsync",
            [
                nameof(Query<BehaveNote>.CompilePurge),
                nameof(Query<BehaveNote>.RenderPurge),
                nameof(Query<BehaveNote>.PurgeAsync),
            ],
            false, false,
            async q =>
            {
                q.CompilePurge();
                q.RenderPurge();
                await q.PurgeAsync();
                q.CompilePurge([Probe], Probe);
                q.RenderPurge([Probe], Probe);
                await q.PurgeAsync([Probe], Probe);
            }),
        new("RestoreAsync",
            [
                nameof(Query<BehaveNote>.CompileRestore),
                nameof(Query<BehaveNote>.RenderRestore),
                nameof(Query<BehaveNote>.RestoreAsync),
            ],
            false, false,
            async q =>
            {
                q.CompileRestore([Probe], Probe);
                q.RenderRestore([Probe], Probe);
                await q.RestoreAsync([Probe], Probe);
            }),
        new("InsertManyAsync",
            [
                nameof(Query<BehaveNote>.CompileInsertMany),
                nameof(Query<BehaveNote>.RenderInsertMany),
                nameof(Query<BehaveNote>.InsertManyAsync),
            ],
            false, false,
            async q =>
            {
                q.CompileInsertMany([NewRow()]);
                q.RenderInsertMany([NewRow()]);
                await q.InsertManyAsync([NewRow()]);
            }),
        new("GroupBy",
            [nameof(Query<BehaveNote>.GroupBy)],
            false, false,
            async q =>
            {
                var grouping = q.GroupBy(n => n.Stars);
                grouping.Count();
                grouping.CompileSelect();
                grouping.RenderSelect();
                await grouping.ToListAsync();
            }),
        new("UpdateReturningAsync",
            [
                nameof(Query<BehaveNote>.CompileUpdateReturning),
                nameof(Query<BehaveNote>.RenderUpdateReturning),
                nameof(Query<BehaveNote>.UpdateReturningAsync),
            ],
            true, false,
            async q =>
            {
                q.CompileUpdateReturning(Id);
                q.RenderUpdateReturning(Id);
                await q.UpdateReturningAsync(Id);
            }),
        new("DeleteReturningAsync",
            [
                nameof(Query<BehaveNote>.CompileDeleteReturning),
                nameof(Query<BehaveNote>.RenderDeleteReturning),
                nameof(Query<BehaveNote>.DeleteReturningAsync),
            ],
            true, false,
            async q =>
            {
                q.CompileDeleteReturning(Id);
                q.RenderDeleteReturning(Id);
                await q.DeleteReturningAsync(Id);
            }),
        new("UpsertAsync",
            [
                nameof(Query<BehaveNote>.CompileUpsert),
                nameof(Query<BehaveNote>.RenderUpsert),
                nameof(Query<BehaveNote>.UpsertAsync),
            ],
            true, false,
            async q =>
            {
                q.CompileUpsert(NewRow(), Id, OnConflict.DoNothing);
                q.RenderUpsert(NewRow(), Id, OnConflict.DoNothing);
                await q.UpsertAsync(NewRow(), Id, OnConflict.DoNothing);
            }),
    ];

    internal static Query<BehaveNote> NewQuery(OrmContext context, bool paged)
    {
        BehaveWorld.Register();
        var tenant = Guid.NewGuid();
        var query = new Query<BehaveNote>(context, BehaveNote.Map).Where(n => n.TenantId == tenant)
            .Redact(n => n.Title);
        return paged
            ? query.OrderBy(n => n.Title).Skip(1).Take(5).ProjectColumn(n => n.Id)
            : query.Set(n => n.Stars, 7).AggregateCount().ProjectColumn(n => n.Id);
    }

    internal static string Snapshot(Query<BehaveNote> query, bool paged)
    {
        var lines = new List<string>
        {
            query.RenderSelect(),
            string.Join(",", query.WhereClauses),
            query.BoundParameters.Count.ToString(),
            query.RenderProjection(),
        };
        if (!paged)
        {
            lines.Add(query.RenderAggregates());
            lines.Add(query.RenderUpdate());
        }
        return string.Join("\n", lines);
    }

    internal static readonly string[] LockedNames =
        ["ToListAsync", "CompileSelect and RenderSelect", "ProjectAsync", "FirstOrDefaultAsync",
            "StreamAsync", "StreamProjectAsync", "StreamProjectionAsync", "StreamColumnAsync"];

    public static IEnumerable<object[]> LockedRows() => LockedNames.Select(name => new object[] { name });

    internal static CensusEntry Entry(string name) => Entries.Single(e => e.Name == name);

    public static IEnumerable<object[]> PostgresRows() => Rows(includePostgresOnly: true);

    public static IEnumerable<object[]> SqliteRows() => Rows(includePostgresOnly: false);

    private static IEnumerable<object[]> Rows(bool includePostgresOnly)
    {
        foreach (var entry in Entries.Where(e => includePostgresOnly || !e.PostgresOnly))
        {
            yield return new object[] { entry.Name, false };
            if (entry.Paged) yield return new object[] { entry.Name, true };
        }
    }
}
