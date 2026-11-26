using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OneToOneBaseline
{
    internal static bool HasIntent(IReadOnlyList<EntityDescriptor> entities) =>
        entities.Any(e => e.OneToOnes.Count != 0);

    internal static IReadOnlyList<Node> Create(IReadOnlyList<EntityDescriptor> entities)
    {
        OneToOneValidation.Records(entities);
        ForeignKeyBaseline.ValidateEntities(entities);
        var extra = entities.ToDictionary(e => e.Table, _ => new List<ForeignKeyDescriptor>(), StringComparer.Ordinal);
        var unique = new List<CreateIndex>();
        var names = entities.SelectMany(e => e.Indexes.Select(i => i.Name))
            .Concat(entities.SelectMany(e => e.ForeignKeys.Select(k => k.Name)))
            .Concat(entities.SelectMany(e => e.OneToOnes.Select(r => r.Constraints.ForeignKeyName)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var owner in entities) AddOwner(owner, entities, extra, unique, names);
        OneToOneValidation.Inverses(entities);
        var tables = ForeignKeyBaseline.Create(entities, extra);
        return tables.Concat(unique.OrderBy(i => i.Index, StringComparer.Ordinal)).ToArray();
    }

    private static void AddOwner(EntityDescriptor owner, IReadOnlyList<EntityDescriptor> entities,
        Dictionary<string, List<ForeignKeyDescriptor>> extra, List<CreateIndex> unique, HashSet<string> names)
    {
        var seen = new Dictionary<string, OneToOneDescriptor>(StringComparer.Ordinal);
        foreach (var relation in owner.OneToOnes)
        {
            var target = OneToOneValidation.Target(owner, relation, entities);
            if (seen.TryGetValue(relation.Owner.ForeignKeyColumn, out var prior))
            {
                if (prior != relation) throw OneToOneValidation.Fail(OneToOneError.AmbiguousPair, owner);
                continue;
            }
            seen.Add(relation.Owner.ForeignKeyColumn, relation);
            if (!names.Add(relation.Constraints.UniqueName))
                throw OneToOneValidation.Fail(OneToOneError.ConflictingConstraint, owner);
            var fk = new ForeignKeyDescriptor(relation.Constraints.ForeignKeyName, relation.Owner.ForeignKeyColumn,
                new ForeignKeyTarget(target.Table, relation.Target.KeyColumn), relation.Constraints.OnDelete);
            extra[owner.Table].Add(fk);
            unique.Add(new CreateIndex(relation.Constraints.UniqueName, owner.Table,
                [new ColumnRef(relation.Owner.ForeignKeyColumn)], Unique: true));
        }
    }
}
