using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OneToOneLookupValidation
{
    internal static void Require<T, TKey>(OneToOneLookup<T, TKey> lookup) where T : class where TKey : notnull
    {
        var target = lookup.Target;
        if (target is null || !ReferenceEquals(OrmMeta.ReadDescriptor(typeof(T)), target.Entity)
            || !OneToOneReadGuard.Eligible(typeof(T), lookup.Map))
            throw new OneToOneDeclarationException(OneToOneError.InvalidOwner, typeof(T).FullName);
        var relation = Relations(target).FirstOrDefault();
        if (relation is null) throw OneToOneValidation.Fail(OneToOneError.InvalidInverse, target.Entity);
        if (target.Direction == OneToOneDirection.PrincipalToOwner && target.Entity.Soft is not null)
            throw OneToOneValidation.Fail(OneToOneError.UnsupportedVisibility, target.Entity);
        var column = target.Entity.Columns.SingleOrDefault(c => c.Column == target.MatchColumn);
        if (column is null || !KeyMatches(typeof(TKey), column.Type))
            throw OneToOneValidation.Fail(OneToOneError.IncompatibleKey, target.Entity);
    }

    private static IEnumerable<OneToOneDescriptor> Relations(OneToOneLookupTarget target) => target.Direction switch
    {
        OneToOneDirection.OwnerToPrincipal => OneToOneReadGuard.Descriptors.SelectMany(d => d.OneToOnes)
            .Where(r => r.Target.Entity == target.Entity.ClrType && r.Target.OwnerNavigation == target.Navigation
                && r.Target.KeyColumn == target.MatchColumn),
        OneToOneDirection.PrincipalToOwner => target.Entity.OneToOnes
            .Where(r => r.Target.InverseNavigation == target.Navigation
                && r.Owner.ForeignKeyColumn == target.MatchColumn),
        _ => [],
    };

    private static bool KeyMatches(Type type, ColumnType column) => column switch
    {
        ColumnType.Uuid => type == typeof(Guid), ColumnType.Int32 => type == typeof(int),
        ColumnType.Int64 => type == typeof(long), ColumnType.Bool => type == typeof(bool),
        ColumnType.Double => type == typeof(double), ColumnType.Decimal => type == typeof(decimal),
        ColumnType.Timestamp => type == typeof(DateTime), ColumnType.Date => type == typeof(DateOnly),
        ColumnType.Text => type == typeof(string) || type.IsEnum,
        _ => false,
    };
}
