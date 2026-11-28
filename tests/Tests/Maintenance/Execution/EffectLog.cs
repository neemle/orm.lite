namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// Every effect the doubles saw, in order: "gate", "open", "exec <text>", "dispose <what>", "release gate". A test reads
// it to prove what was dispatched and that cleanup ran once, last first.
internal sealed class EffectLog
{
    private readonly List<string> _entries = [];

    internal IReadOnlyList<string> Entries
    {
        get
        {
            lock (_entries) return [.. _entries];
        }
    }

    internal void Add(string entry)
    {
        lock (_entries) _entries.Add(entry);
    }

    internal int Count(string entry) => Entries.Count(e => e == entry);

    // The entries after the last one equal to `marker`.
    internal IReadOnlyList<string> After(string marker)
    {
        var entries = Entries;
        var at = entries.ToList().LastIndexOf(marker);
        return [.. entries.Skip(at + 1)];
    }
}
