using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Collection = Neemle.OrmLite.Generator.OrmGenerator.Collection;

namespace Neemle.OrmLite.Generator.Orphans;

internal static class OrphanCollectionReader
{
    internal static IEnumerable<OrphanCollectionModel> All(ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked)
    {
        foreach (var parent in entities)
            foreach (var collection in parent.Collections)
            {
                if (blocked.Contains((parent.Full, collection.Nav))
                    || !parent.OrphanSafety!.Collections.Contains(collection.Nav)) continue;
                var child = entities.SingleOrDefault(e => e.Full == collection.ChildFull);
                if (child is not null && Valid(parent, child, collection))
                    yield return new(parent, child, collection.Nav, collection.InverseFk);
            }
    }

    private static bool Valid(Entity parent, Entity child, Collection collection)
    {
        var owner = child.Props.SingleOrDefault(p => p.Name == collection.InverseFk);
        var key = OrmGenerator.FirstKey(parent)!;
        if (owner is null || owner.IsKey || owner.Cs != key.Cs) return false;
        if (child.Props.Count(p => p.Name == owner.Name || p.Column == owner.Name) != 1) return false;
        var foreign = child.ForeignKeys.Where(f => f.Column == owner.Column).ToArray();
        return foreign.Length == 1 && foreign[0].TargetTable == parent.Table
            && foreign[0].TargetColumn == key.Column;
    }
}
