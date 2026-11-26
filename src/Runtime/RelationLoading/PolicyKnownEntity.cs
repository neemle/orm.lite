namespace Neemle.OrmLite;

internal sealed class PolicyKnownEntity(object entity, IGraphEntityBinding binding)
{
    internal WeakReference<object> Entity { get; } = new(entity);
    internal IGraphEntityBinding Binding { get; } = binding;
    private readonly object _identity = binding.Key(entity);

    internal void Seed(GraphReadContext context)
    {
        if (Entity.TryGetTarget(out var entity) && Binding.Comparer.Equals(_identity, Binding.Key(entity)))
            context.Identity.Intern(Binding, entity);
    }
}
