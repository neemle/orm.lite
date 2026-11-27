using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteOrphanDefinitions
{
    internal static string Text(SqliteProvider provider, Node node) =>
        provider.CreateSerializer().Serialize(node).Text.Trim().TrimEnd(';');

    internal static void Table(SqliteProvider provider, EntityDescriptor entity,
        IReadOnlyList<SqliteOrphanObject> objects)
    {
        var actual = objects.SingleOrDefault(o => o.Type == "table");
        if (actual is null) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        if (provider.CreateSerializer() is not ISchemaDiffDdlSerializer serializer)
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var table = EntityStatements.CreateTables(entity).OfType<CreateTable>().Single();
        var valid = new[] { false, true }.Select(explicitUpdate => serializer.SerializeStrictCreate(
            table with { Constraints = ForeignKeys(entity, explicitUpdate) }).Text.Trim().TrimEnd(';'));
        if (!valid.Contains(actual.Sql.Trim().TrimEnd(';'), StringComparer.Ordinal))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private static NodeList ForeignKeys(EntityDescriptor entity, bool explicitUpdate) =>
        NodeList.Of(entity.ForeignKeys.Select(f => (Node)new ForeignKeyDef(f.Name, [new ColumnRef(f.LocalColumn)],
            f.Target.Table, [new ColumnRef(f.Target.Column)], f.OnDelete,
            explicitUpdate ? ReferentialAction.NoAction : null)));

    internal static void Indexes(SqliteProvider provider, EntityDescriptor entity,
        IReadOnlyList<SqliteOrphanObject> objects)
    {
        var expected = EntityStatements.CreateIndexes(entity).Select(n => Text(provider, n)).ToArray();
        if (objects.Where(o => o.Type == "index").Any(o =>
            !expected.Contains(o.Sql.Trim().TrimEnd(';'), StringComparer.Ordinal)))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }
}
