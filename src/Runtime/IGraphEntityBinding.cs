using System.Data.Common;

namespace Neemle.OrmLite;

internal interface IGraphEntityBinding
{
    Type EntityType { get; }
    EntityDescriptor Descriptor { get; }
    string KeyColumn { get; }
    Type KeyType { get; }
    IEqualityComparer<object> Comparer { get; }
    IReadOnlyList<IGraphNavigationBinding> Navigations { get; }
    object Key(object value);
    object ReadKey(DbDataReader reader, int ordinal);
    object Map(DbDataReader reader);
    bool Maps(Delegate map);
    bool Equivalent(IGraphEntityBinding other);
}
