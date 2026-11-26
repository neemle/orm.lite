namespace Neemle.OrmLite;

internal static class SchemaTargetCopies
{
    internal static SchemaDiffTarget Capture(SchemaDiffTarget target)
    {
        _ = SchemaDiffSnapshot.Capture(target);
        var entities = SchemaDiffNames.Limited(target.Schema.Entities, target.Limits.MaxCatalogObjects)
            .Select(e => Entity(e, target.Limits)).ToArray();
        var copied = target with { Schema = OrmSchema.Of(target.Schema.Token, entities),
            Scope = target.Scope with { OwnedTables = Array.AsReadOnly(target.Scope.OwnedTables.ToArray()) },
            Limits = target.Limits with { } };
        _ = SchemaDiffSnapshot.Capture(copied);
        return copied;
    }

    private static EntityDescriptor Entity(EntityDescriptor source, SchemaDiffLimits limits)
    {
        var entity = EntityDescriptor.Of(source.ClrType, source.Table, source.Managed);
        foreach (var column in SchemaDiffNames.Limited(source.Columns, limits.MaxColumns))
            entity.Property(column.Property, column.Column, column.Type, column.Nullable, column.Key,
                column.Precision, column.Scale);
        if (source.IsView) entity.View();
        foreach (var index in SchemaDiffNames.Limited(source.Indexes, limits.MaxCatalogObjects))
            entity.Index(index.Name, index.Unique, index.LiveOnly,
                SchemaDiffNames.Limited(index.Terms, limits.MaxColumns).ToArray())
                .Filter(SchemaDiffNames.Limited(index.Filter, limits.MaxColumns).ToArray());
        foreach (var link in SchemaDiffNames.Limited(source.ManyToMany, limits.MaxCatalogObjects))
            entity.Link(link.Navigation, link.JoinTable, link.ThisKey, link.OtherKey, link.OtherTable,
                link.OtherKeyColumn, link.OtherSoftColumn, link.Pure);
        if (source.Soft is { } soft) Soft(entity, soft, limits);
        return entity;
    }

    private static void Soft(EntityDescriptor entity, SoftDeletion soft, SchemaDiffLimits limits)
    {
        entity.SoftDelete(soft.Property, soft.Column, soft.Token);
        if (!soft.Platform) entity.Tenant(soft.TenantColumn!);
        if (soft.LabelColumn is { } label) entity.Label(label);
        entity.Unversioned(SchemaDiffNames.Limited(soft.Unversioned, limits.MaxColumns).ToArray());
        entity.Owners(SchemaDiffNames.Limited(soft.Owners, limits.MaxCatalogObjects).ToArray());
        entity.Pinned(SchemaDiffNames.Limited(soft.Pinned, limits.MaxCatalogObjects).ToArray());
        entity.StatusColumns(SchemaDiffNames.Limited(soft.StatusColumns, limits.MaxColumns).ToArray());
    }
}
