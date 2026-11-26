namespace Neemle.OrmLite;

internal sealed record PolicyFrontier(IGraphEntityBinding Binding, IReadOnlyList<object> Parents,
    int Depth, string Path);
