using System.Data.Common;

namespace Neemle.OrmLite;

internal interface IOrphanEntity
{
    Type EntityType { get; }
    Type KeyType { get; }
    OrphanMetadata Metadata { get; }
    object Key(object entity);
    object ReadKey(DbDataReader reader, int ordinal);
    object?[] Values(object entity);
    IEqualityComparer<object> KeyComparer { get; }
    bool Equivalent(IOrphanEntity other);
    void ValidateCurrent();
}
