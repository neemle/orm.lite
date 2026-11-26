namespace Neemle.OrmLite;

public abstract class GraphKeyBinding<T> where T : class
{
    internal GraphKeyBinding() { }
    internal abstract bool Equivalent(GraphKeyBinding<T> other);
    internal abstract string Column { get; }
    internal abstract Type KeyType { get; }
    internal abstract object Read(T value);
    internal abstract object Read(System.Data.Common.DbDataReader reader, int ordinal);
    internal abstract IEqualityComparer<object> Comparer { get; }
}
