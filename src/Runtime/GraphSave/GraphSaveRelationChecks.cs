namespace Neemle.OrmLite;

internal static class GraphSaveRelationChecks
{
    internal static void Validate(IGraphSaveEntity source, IGraphSaveEntity target, IGraphSaveRelation relation)
    {
        if (source.EntityType != relation.SourceType || target.EntityType != relation.TargetType)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        if (relation.Shape.Kind == GraphSaveRelationKind.Link) { Link(source, target, relation); return; }
        var owner = relation.Shape.ForeignKeyOwner == GraphSaveForeignKeyOwner.Source ? source : target;
        var principal = ReferenceEquals(owner, source) ? target : source;
        var ordinal = GraphSaveColumns.Find(owner.Descriptor, relation.Shape.ForeignKeyProperty!);
        var column = owner.Descriptor.Columns[ordinal];
        if (column.Key || !GraphSaveShapes.Accepts(column.Type, principal.KeyType))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal static ManyToManyDescriptor Link(IGraphSaveEntity source, IGraphSaveEntity target,
        IGraphSaveRelation relation)
    {
        var link = source.Descriptor.ManyToMany.SingleOrDefault(l => l.Navigation == relation.Shape.Navigation);
        if (link is null || !link.Pure || source.KeyType != typeof(Guid) || target.KeyType != typeof(Guid)
            || link.OtherTable != target.Descriptor.Table || link.OtherKeyColumn != target.Descriptor.SoleKey!.Column
            || link.ThisKey == link.OtherKey || string.IsNullOrWhiteSpace(link.JoinTable))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        return link;
    }
}
