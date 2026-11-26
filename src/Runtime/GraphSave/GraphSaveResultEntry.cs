using System.Collections.ObjectModel;

namespace Neemle.OrmLite;

internal sealed class GraphSaveResultEntry(bool written, IReadOnlyDictionary<string, object> bindings)
{
    internal bool Written { get; } = written;
    internal IReadOnlyDictionary<string, object> Bindings { get; } = new ReadOnlyDictionary<string, object>(
        bindings.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
}
