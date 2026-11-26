using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class ForeignKeyOrder
{
    internal static IReadOnlyList<Node> Sort(IReadOnlyDictionary<string, CreateTable> tables,
        IReadOnlyDictionary<string, List<ForeignKeyDescriptor>> keys)
    {
        var parents = keys.ToDictionary(p => p.Key,
            p => p.Value.Select(k => k.Target.Table).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var children = tables.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var pair in parents)
            foreach (var parent in pair.Value) children[parent].Add(pair.Key);
        var ready = new SortedSet<string>(parents.Where(p => p.Value.Count == 0).Select(p => p.Key),
            StringComparer.Ordinal);
        var result = new List<Node>();
        while (ready.Count > 0)
        {
            var name = ready.Min!;
            ready.Remove(name);
            result.Add(Constrained(tables[name], keys[name]));
            foreach (var child in children[name])
                if (parents[child].Remove(name) && parents[child].Count == 0) ready.Add(child);
        }
        if (result.Count != tables.Count)
            throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.DependencyCycle,
                string.Join(",", parents.Where(p => p.Value.Count > 0).Select(p => p.Key)
                    .Order(StringComparer.Ordinal)),
                string.Join(",", keys.Where(p => parents[p.Key].Count > 0).SelectMany(p => p.Value)
                    .Select(k => k.Name).Order(StringComparer.Ordinal)));
        return result;
    }

    private static CreateTable Constrained(CreateTable table, IEnumerable<ForeignKeyDescriptor> keys) =>
        table with { Constraints = NodeList.Of(keys.OrderBy(k => k.Name, StringComparer.Ordinal).Select(NodeOf)) };

    private static Node NodeOf(ForeignKeyDescriptor key) => new ForeignKeyDef(key.Name,
        [new ColumnRef(key.LocalColumn)], key.Target.Table, [new ColumnRef(key.Target.Column)], key.OnDelete);
}
