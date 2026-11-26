namespace Neemle.OrmLite;

internal sealed class PolicyModel(GraphLimits limits, IReadOnlyDictionary<IGraphNavigationBinding, RelationLoadMode> modes)
{
    internal GraphLimits Limits { get; } = limits;
    internal IReadOnlyDictionary<IGraphNavigationBinding, RelationLoadMode> Modes { get; } = modes;
    internal IEnumerable<IGraphNavigationBinding> Eager => Modes.Where(p => p.Value == RelationLoadMode.Eager)
        .Select(p => p.Key);

    internal bool Equivalent(PolicyModel other) => Limits == other.Limits && Modes.Count == other.Modes.Count
        && Modes.All(p => other.Modes.TryGetValue(p.Key, out var mode) && p.Value == mode);
}
