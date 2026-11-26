using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class ForeignKeyBaseline
{
    internal static bool HasIntent(IEnumerable<EntityDescriptor> entities) =>
        entities.Any(e => e.ForeignKeys.Count > 0 || e.LinkForeignKeys.Count > 0);

    internal static IReadOnlyList<Node> Create(IReadOnlyList<EntityDescriptor> entities)
    {
        var known = Entities(entities);
        var tables = entities.Where(e => !e.IsView).SelectMany(EntityStatements.CreateTables).Cast<CreateTable>()
            .ToDictionary(t => t.Table, StringComparer.Ordinal);
        var owners = new Dictionary<string, EntityDescriptor>(known, StringComparer.Ordinal);
        AddJoins(entities, tables, owners);
        var keys = tables.Keys.ToDictionary(t => t, _ => new List<ForeignKeyDescriptor>(), StringComparer.Ordinal);
        var names = new Dictionary<string, (string Table, ForeignKeyDescriptor Key)>(StringComparer.OrdinalIgnoreCase);
        foreach (var owner in entities)
        {
            foreach (var key in owner.ForeignKeys) Add(owner, key, known, keys, names);
            foreach (var intent in owner.LinkForeignKeys)
            {
                var key = ForeignKeyValidation.Link(owner, intent, known);
                var link = owner.ManyToMany.Single(l => l.Navigation == intent.Navigation);
                Add(owners[link.JoinTable], key, known, keys, names);
            }
        }
        return ForeignKeyOrder.Sort(tables, keys);
    }

    private static Dictionary<string, EntityDescriptor> Entities(IEnumerable<EntityDescriptor> entities)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, EntityDescriptor>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            ForeignKeyValidation.SchemaEntity(entity);
            if (!names.Add(entity.Table))
                throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.ConflictingDeclaration, entity.Table);
            result.Add(entity.Table, entity);
        }
        return result;
    }

    private static void AddJoins(IEnumerable<EntityDescriptor> entities, Dictionary<string, CreateTable> tables,
        Dictionary<string, EntityDescriptor> owners)
    {
        foreach (var owner in entities.Where(e => e.Managed && !e.IsView))
            foreach (var table in EntityStatements.CreateJoinTables(owner).Cast<CreateTable>())
                AddJoin(owner, table, entities, tables, owners);
    }

    private static void AddJoin(EntityDescriptor owner, CreateTable table, IEnumerable<EntityDescriptor> entities,
        Dictionary<string, CreateTable> tables, Dictionary<string, EntityDescriptor> owners)
    {
        ForeignKeyValidation.Name(table.Table, owner.Table);
        var columns = table.Columns.OfType<ColumnDef>().OrderBy(c => c.Column, StringComparer.Ordinal).ToArray();
        foreach (var column in columns) ForeignKeyValidation.Name(column.Column, owner.Table);
        if (entities.Any(e => string.Equals(e.Table, table.Table, StringComparison.OrdinalIgnoreCase)))
            throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.UnsupportedLink, owner.Table);
        if (owners.Keys.Any(t => t != table.Table
            && string.Equals(t, table.Table, StringComparison.OrdinalIgnoreCase)))
            throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.UnsupportedLink, owner.Table);
        if (owners.TryGetValue(table.Table, out var prior))
        {
            if (!prior.ColumnNames.SequenceEqual(columns.Select(c => c.Column)))
                throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.UnsupportedLink, owner.Table);
            return;
        }
        var descriptor = EntityDescriptor.Of(table.Table, table.Table, true);
        foreach (var c in columns) descriptor.Property(c.Column, c.Column, ColumnType.Uuid, false, true);
        ForeignKeyValidation.Shape(descriptor);
        owners.Add(table.Table, descriptor);
        tables.Add(table.Table, table with { Columns = NodeList.Of(columns.Cast<Node>().Append(
            new TablePrimaryKey(NodeList.Of(columns.Select(c => (Node)new ColumnRef(c.Column)))))) });
    }

    private static void Add(EntityDescriptor owner, ForeignKeyDescriptor key,
        IReadOnlyDictionary<string, EntityDescriptor> entities, Dictionary<string, List<ForeignKeyDescriptor>> keys,
        Dictionary<string, (string Table, ForeignKeyDescriptor Key)> names)
    {
        ForeignKeyValidation.Endpoints(owner, key, entities);
        var existing = keys[owner.Table].FirstOrDefault(k => k.LocalColumn == key.LocalColumn
            && k.Target == key.Target);
        if (existing is not null)
        {
            if (existing != key)
                throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.ConflictingDeclaration, owner.Table);
            return;
        }
        if (names.ContainsKey(key.Name))
            throw ForeignKeyValidation.Fail(ForeignKeyDeclarationError.DuplicateName, owner.Table);
        names.Add(key.Name, (owner.Table, key));
        keys[owner.Table].Add(key);
    }
}
