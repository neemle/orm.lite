using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

internal static class ClauseSeed
{
    internal static readonly int[] Scrambled = [3, 1, 5, 2, 4];

    internal static Query<BehaveNote> Notes(OrmContext context) => new(context, BehaveNote.Map);

    internal static Query<BehaveCounter> Counters(OrmContext context) => new(context, BehaveCounter.Map);

    internal static async Task<Guid> NotesAsync(OrmContext context, IEnumerable<int>? stars = null)
    {
        var tenant = Guid.NewGuid();
        var rows = (stars ?? Scrambled).Select(star => new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, tenant)
            .With(n => n.Title, "t" + star)
            .With(n => n.Stars, star)).ToList();
        await Notes(context).InsertManyAsync(rows);
        return tenant;
    }

    internal static async Task<List<Guid>> CountersAsync(OrmContext context)
    {
        var ids = Scrambled.Select(_ => Guid.NewGuid()).ToList();
        var rows = ids.Select((id, i) => new Row<BehaveCounter>()
            .With(c => c.Id, id)
            .With(c => c.N, Scrambled[i])).ToList();
        await Counters(context).InsertManyAsync(rows);
        return ids;
    }

    internal static async Task<string> NotesSnapshotAsync(OrmContext context, Guid tenant)
    {
        var rows = await Notes(context).IncludingDeleted().Where(n => n.TenantId == tenant)
            .OrderBy(n => n.Title).ToListAsync();
        return string.Join("|", rows.Select(n => $"{n.Title}:{n.Stars}:{n.DeletedAt is null}"));
    }

    internal static async Task<string> CountersSnapshotAsync(OrmContext context, List<Guid> ids)
    {
        var rows = await Counters(context).Where(c => ids.Contains(c.Id)).OrderBy(c => c.N).ToListAsync();
        return string.Join("|", rows.Select(c => c.N));
    }
}
