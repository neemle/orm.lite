#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class ForeignKeyValidation
{
    internal static bool ValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl) && Unicode(name);

    internal static void Name(string? name, string entity)
    {
        if (!ValidName(name)) throw Fail(ForeignKeyDeclarationError.InvalidName, entity);
    }

    internal static ForeignKeyDeclarationException Fail(ForeignKeyDeclarationError error,
        string? entity, string? navigation = null) => new(error, entity, navigation);

    internal static void Action(ReferentialAction action, string entity)
    {
        if (action is not (ReferentialAction.NoAction or ReferentialAction.Restrict
            or ReferentialAction.Cascade or ReferentialAction.SetNull))
            throw Fail(ForeignKeyDeclarationError.InvalidDeleteAction, entity);
    }

    internal static void Shape(EntityDescriptor entity)
    {
        Name(entity.Table, entity.ClrType);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in entity.Columns)
        {
            Name(column.Column, entity.Table);
            if (!columns.Add(column.Column)) throw Fail(ForeignKeyDeclarationError.UnmappedColumn, entity.Table);
        }
    }

    internal static void SchemaEntity(EntityDescriptor entity)
    {
        Shape(entity);
        if (entity.Managed && !entity.IsView && entity.Columns.Count(c => c.Key) > 1)
            throw Fail(ForeignKeyDeclarationError.UnsupportedKey, entity.Table);
    }

    internal static void Endpoints(EntityDescriptor owner, ForeignKeyDescriptor key,
        IReadOnlyDictionary<string, EntityDescriptor> entities)
    {
        if (key.Target is null) throw Fail(ForeignKeyDeclarationError.UnsupportedKey, owner.Table);
        Name(key.Name, owner.Table);
        Name(key.LocalColumn, owner.Table);
        Name(key.Target.Table, owner.Table);
        Name(key.Target.Column, owner.Table);
        Action(key.OnDelete, owner.Table);
        Managed(owner);
        if (!entities.TryGetValue(key.Target.Table, out var target))
            throw Fail(ForeignKeyDeclarationError.ExternalBaseline, owner.Table);
        Managed(target);
        var source = owner.Columns.SingleOrDefault(c => c.Column == key.LocalColumn)
            ?? throw Fail(ForeignKeyDeclarationError.UnmappedColumn, owner.Table);
        var primary = target.SoleKey;
        if (primary is null || !ValidPrimary(primary) || primary.Column != key.Target.Column)
            throw Fail(ForeignKeyDeclarationError.UnsupportedKey, owner.Table);
        if ((source.Type, source.Precision, source.Scale) != (primary.Type, primary.Precision, primary.Scale))
            throw Fail(ForeignKeyDeclarationError.IncompatibleKey, owner.Table);
        if (key.OnDelete == ReferentialAction.SetNull && !source.Nullable)
            throw Fail(ForeignKeyDeclarationError.NonNullableSetNull, owner.Table);
    }

    private static bool ValidPrimary(ColumnDescriptor key) => !key.Nullable && Enum.IsDefined(key.Type);

    internal static void Managed(EntityDescriptor entity)
    {
        if (!entity.Managed || entity.IsView)
            throw Fail(ForeignKeyDeclarationError.UnmanagedEndpoint, entity.Table);
    }

    internal static ForeignKeyDescriptor Link(EntityDescriptor owner, EntityDescriptor.LinkIntent intent,
        IReadOnlyDictionary<string, EntityDescriptor> entities)
    {
        Name(intent.Navigation, owner.Table);
        var link = owner.ManyToMany.FirstOrDefault(l => l.Navigation == intent.Navigation)
            ?? throw Fail(ForeignKeyDeclarationError.UnknownRelation, owner.Table, intent.Navigation);
        if (owner.ManyToMany.Count(l => l.Navigation == intent.Navigation) != 1)
            throw Fail(ForeignKeyDeclarationError.UnknownRelation, owner.Table, intent.Navigation);
        if (intent.End is not (LinkForeignKeyEnd.Source or LinkForeignKeyEnd.Target))
            throw Fail(ForeignKeyDeclarationError.UnsupportedLink, owner.Table, intent.Navigation);
        if (!link.Pure || entities.ContainsKey(link.JoinTable))
            throw Fail(ForeignKeyDeclarationError.UnsupportedLink, owner.Table, intent.Navigation);
        Managed(owner);
        if (!entities.TryGetValue(link.OtherTable, out var target))
            throw Fail(ForeignKeyDeclarationError.ExternalBaseline, owner.Table, intent.Navigation);
        Managed(target);
        GuidLink(owner, target, link);
        var sourceEnd = intent.End == LinkForeignKeyEnd.Source;
        if (owner.Table == target.Table)
            throw Fail(ForeignKeyDeclarationError.DependencyCycle, owner.Table, intent.Navigation);
        var endpoint = sourceEnd ? owner : target;
        return new ForeignKeyDescriptor(intent.Declaration.Name, sourceEnd ? link.ThisKey : link.OtherKey,
            new ForeignKeyTarget(endpoint.Table, endpoint.SoleKey!.Column), intent.Declaration.OnDelete);
    }

    private static void GuidLink(EntityDescriptor owner, EntityDescriptor target, ManyToManyDescriptor link)
    {
        if (owner.SoleKey is not { Type: ColumnType.Uuid, Nullable: false }
            || target.SoleKey is not { Type: ColumnType.Uuid, Nullable: false } other
            || other.Column != link.OtherKeyColumn)
            throw Fail(ForeignKeyDeclarationError.UnsupportedLink, owner.Table, link.Navigation);
    }

    private static bool Unicode(string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            if (!char.IsSurrogate(name[i])) continue;
            if (!char.IsHighSurrogate(name[i]) || i + 1 == name.Length || !char.IsLowSurrogate(name[i + 1]))
                return false;
            i++;
        }
        return true;
    }
}
