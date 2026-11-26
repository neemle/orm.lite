using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OneToOneValidation
{
    internal static EntityDescriptor Target(EntityDescriptor owner, OneToOneDescriptor relation,
        IReadOnlyList<EntityDescriptor> entities)
    {
        if (relation.Owner is null || relation.Target is null || relation.Constraints is null)
            throw Fail(OneToOneError.InvalidOwner, owner);
        if (relation.Owner.Entity != owner.ClrType) throw Fail(OneToOneError.InvalidOwner, owner);
        var targets = entities.Where(e => e.ClrType == relation.Target.Entity).ToArray();
        if (targets.Length != 1) throw Fail(OneToOneError.UnsupportedSchema, owner);
        var target = targets[0];
        if (!owner.Managed || owner.IsView || !target.Managed || target.IsView || owner == target)
            throw Fail(OneToOneError.UnsupportedSchema, owner);
        if (owner.SoleKey is not { Nullable: false } || target.SoleKey is not { Nullable: false } key
            || key.Column != relation.Target.KeyColumn) throw Fail(OneToOneError.UnsupportedKey, owner);
        var source = owner.Columns.SingleOrDefault(c => c.Property == relation.Owner.ForeignKeyProperty);
        if (source is null || source.Column != relation.Owner.ForeignKeyColumn)
            throw Fail(OneToOneError.UnmappedForeignKey, owner);
        if ((source.Type, source.Precision, source.Scale) != (key.Type, key.Precision, key.Scale))
            throw Fail(OneToOneError.IncompatibleKey, owner);
        Names(owner, relation);
        return target;
    }

    internal static void Inverses(IReadOnlyList<EntityDescriptor> entities)
    {
        var pairs = entities.SelectMany(e => e.OneToOnes);
        foreach (var group in pairs.GroupBy(r => (r.Target.Entity, r.Target.InverseNavigation)))
            if (group.Select(r => (r.Owner.Entity, r.Owner.ForeignKeyColumn)).Distinct().Count() != 1)
                throw new OneToOneDeclarationException(OneToOneError.AmbiguousPair, group.Key.Entity,
                    group.Key.InverseNavigation);
    }

    private static void Names(EntityDescriptor owner, OneToOneDescriptor relation)
    {
        var names = new[] { relation.Target.OwnerNavigation, relation.Target.InverseNavigation,
            relation.Constraints.ForeignKeyName, relation.Constraints.UniqueName };
        if (names.Any(n => !ForeignKeyValidation.ValidName(n)))
            throw Fail(OneToOneError.ConflictingConstraint, owner);
    }

    internal static OneToOneDeclarationException Fail(OneToOneError error, EntityDescriptor entity) =>
        new(error, entity.ClrType);
}
