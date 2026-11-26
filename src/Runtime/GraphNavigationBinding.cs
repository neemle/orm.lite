namespace Neemle.OrmLite;

public abstract class GraphNavigationBinding<TSource> : IGraphNavigationBinding where TSource : class
{
    internal GraphNavigationBinding() { }
    internal abstract Type Target { get; }
    internal abstract string Navigation { get; }
    internal abstract string? SourceColumn { get; }
    internal abstract string? TargetColumn { get; }
    internal abstract Type SourceKeyType { get; }
    internal abstract Type TargetKeyType { get; }
    internal virtual GraphLinkSource? Link => null;
    internal abstract bool Equivalent(IGraphNavigationBinding other);
    internal abstract void Validate(IGraphEntityBinding source, IGraphEntityBinding target);
    internal abstract Task<List<object>> LoadAsync(GraphReadContext context,
        GraphPlanNode node, IReadOnlyList<object> parents);

    Type IGraphNavigationBinding.Source => typeof(TSource);
    Type IGraphNavigationBinding.Target => Target;
    string IGraphNavigationBinding.Navigation => Navigation;
    string? IGraphNavigationBinding.SourceColumn => SourceColumn;
    string? IGraphNavigationBinding.TargetColumn => TargetColumn;
    Type IGraphNavigationBinding.SourceKeyType => SourceKeyType;
    Type IGraphNavigationBinding.TargetKeyType => TargetKeyType;
    GraphLinkSource? IGraphNavigationBinding.Link => Link;
    bool IGraphNavigationBinding.Equivalent(IGraphNavigationBinding other) => Equivalent(other);
    void IGraphNavigationBinding.Validate(IGraphEntityBinding source, IGraphEntityBinding target) =>
        Validate(source, target);
    Task<List<object>> IGraphNavigationBinding.LoadAsync(GraphReadContext context,
        GraphPlanNode node, IReadOnlyList<object> parents) => LoadAsync(context, node, parents);

    internal static void Require(bool condition)
    { if (!condition) throw new GraphRequestException(GraphRequestError.InvalidBinding); }

    internal static bool HasColumn(IGraphEntityBinding binding, string? column) =>
        binding.Descriptor.Columns.Any(c => c.Column == column);
}
