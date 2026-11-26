namespace Neemle.OrmLite;

internal interface IGraphSaveEntity
{
    Type EntityType { get; }
    Type KeyType { get; }
    EntityDescriptor Original { get; }
    EntityDescriptor Descriptor { get; }
    Delegate Mapper { get; }
    IEqualityComparer<object> KeyComparer { get; }
    IReadOnlyList<IGraphSaveRelation> Relations { get; }
    object Key(object entity);
    object?[] Values(object entity);
    bool Equivalent(IGraphSaveEntity other);
}
