namespace Neemle.OrmLite;

internal sealed record PolicyPublication(object Parent, IGraphNavigationBinding Edge, object? Value, Action Apply)
{
    internal bool Empty => Value is null || Value is System.Collections.ICollection { Count: 0 };
}
