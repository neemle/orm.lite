using System.Globalization;

namespace Neemle.OrmLite;

public enum Capability
{
    PartialIndex,
    AlterConstraint,
    ObjectMarker,
    RowHistoryTriggers,
    SessionLock,
    ArrayParameter,
    NullSafeEquality,
    AggregateFilter,
    RowLock,
    KeyPreservingRowLock,
    JsonDocument,
    Upsert,
    Returning,
    IntervalArithmetic,
    NamedLock,
    CatalogGuard,
    ExactDecimal,
    IndexNullsOrdering,
    IndexCollation,
    IndexExpression,
    WalCheckpoint,
}

public enum Support
{
    None,
    Emulated,
    Native,
}

public sealed class CapabilitySet
{
    // The key a manifest line uses for the identifier limit; a provider with no limit prints no such line.
    public const string MaxIdentifierBytesKey = "MaxIdentifierBytes";

    private readonly Dictionary<Capability, Support> _levels;

    private CapabilitySet(Dictionary<Capability, Support> levels, int? maxIdentifierBytes = null)
    {
        _levels = levels;
        MaxIdentifierBytes = maxIdentifierBytes;
    }

    // The most bytes (UTF-8) of an identifier the provider keeps; the server cuts a longer name silently.
    // Null means the provider states no limit.
    public int? MaxIdentifierBytes { get; }

    public static CapabilitySet Everything(Support level = Support.Native)
    {
        var levels = new Dictionary<Capability, Support>();
        foreach (var capability in Enum.GetValues<Capability>()) levels[capability] = level;
        return new CapabilitySet(levels);
    }

    public static CapabilitySet Nothing() => Everything(Support.None);

    public Support Of(Capability capability) =>
        _levels.TryGetValue(capability, out var level) ? level : Support.None;

    public IReadOnlyList<string> Manifest()
    {
        var lines = Enum.GetValues<Capability>().Select(c => c + "=" + Of(c)).ToList();
        if (MaxIdentifierBytes is { } limit)
            lines.Add(MaxIdentifierBytesKey + "=" + limit.ToString(CultureInfo.InvariantCulture));
        return lines;
    }

    public CapabilitySet With(Capability capability, Support level)
    {
        var copy = new Dictionary<Capability, Support>(_levels) { [capability] = level };
        return new CapabilitySet(copy, MaxIdentifierBytes);
    }

    public CapabilitySet WithMaxIdentifierBytes(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        return new CapabilitySet(new Dictionary<Capability, Support>(_levels), limit);
    }
}
