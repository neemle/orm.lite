namespace Neemle.OrmLite;

public sealed class JoinSelection
{
    private readonly object _identity = new();
    private readonly List<JoinSelectionEntry> _entries = [];
    private readonly HashSet<string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public JoinSelection() { }
    public JoinSlot<TValue> Add<TValue>(JoinColumn<TValue> column, string outputAlias) =>
        Append(column, outputAlias, false);
    public JoinSlot<TValue> AddRequired<TValue>(JoinColumn<TValue> column, string outputAlias) =>
        Append(column, outputAlias, true);

    private JoinSlot<TValue> Append<TValue>(JoinColumn<TValue> column, string alias, bool required)
    {
        if (column is null) throw new JoinReadException(JoinError.InvalidProjection);
        column.Reference.Source.Current();
        if (required && column.Reference.Binding.Column.Nullable)
            throw new JoinReadException(JoinError.RequiredNullable);
        JoinAliases.Validate(alias, column.Reference.Source.Input.Database.Provider, JoinError.InvalidProjection);
        if (!_aliases.Add(alias)) throw new JoinReadException(JoinError.DuplicateOutputAlias);
        var slot = new JoinSlot<TValue>(_identity);
        _entries.Add(new(column.Reference, slot, alias, required));
        return slot;
    }

    internal object Identity => _identity;
    internal IReadOnlyList<JoinSelectionEntry> Snapshot() => Array.AsReadOnly(_entries.ToArray());
}
