namespace Neemle.OrmLite;

public sealed class OrphanPresentWrites<T> where T : class
{
    private readonly IReadOnlyList<OrphanPresentWrite<T>> _items;
    private OrphanPresentWrites(IEnumerable<OrphanPresentWrite<T>> items) =>
        _items = Array.AsReadOnly(items.ToArray());

    public static OrphanPresentWrites<T> None { get; } = new([]);
    public OrphanPresentWrites<T> Insert(T child) => Add(child, OrphanPresentIntent.Insert);
    public OrphanPresentWrites<T> Update(T child) => Add(child, OrphanPresentIntent.Update);
    internal IReadOnlyList<OrphanPresentWrite<T>> Items => _items;

    private OrphanPresentWrites<T> Add(T child, OrphanPresentIntent intent)
    {
        if (child is null) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        return new(_items.Append(new(child, intent)));
    }
}
