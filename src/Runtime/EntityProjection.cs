#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class EntityProjection
{
    internal static IEnumerable<Node> Selected(EntityDescriptor? descriptor, string? alias = null)
    {
        if (descriptor is null || descriptor.Calculated.Count == 0) return [new Star(alias)];
        var owner = alias is null ? null : new AliasRef(alias);
        return descriptor.Columns.Select(c => (Node)new ColumnRef(c.Column, owner))
            .Concat(descriptor.Calculated.Select(c => (Node)new Aliased(
                new Call(Fn.Lower, [new ColumnRef(c.SourceColumn, owner)]), c.Alias))).ToList();
    }

    internal static IEnumerable<Node> Redacted(EntityDescriptor descriptor, IReadOnlyDictionary<string, Node> masks)
    {
        foreach (var field in descriptor.Calculated)
            if (masks.ContainsKey(field.SourceColumn))
                throw new NotSupportedException("Cannot read calculated field " + field.Property
                    + " because Redact masks its source. Nothing was executed.");
        return Selected(descriptor).Select(n => n is ColumnRef c && masks.TryGetValue(c.Column, out var mask)
            ? new Aliased(mask, c.Column) : n).ToList();
    }
}
