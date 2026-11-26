using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class OneToOneLookup<T, TKey> where T : class where TKey : notnull
{
    public OneToOneLookup(OneToOneLookupTarget target, TKey key, Func<DbDataReader, T> map)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(map);
        Target = target; Key = key; Map = map;
    }

    internal OneToOneLookupTarget Target { get; }
    internal TKey Key { get; }
    internal Func<DbDataReader, T> Map { get; }
}
