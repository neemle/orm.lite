using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Tests;

public sealed class FixtureProvider : ISqlProvider
{
    public static readonly FixtureProvider Instance = new();

    public string Id => "fixture";

    public CapabilitySet Capabilities { get; } = CapabilitySet.Nothing();

    public ISerializer CreateSerializer() => new FixtureSerializer();
}

public sealed class FixtureSerializer : StandardSerializer
{
    private NotSupportedException Refused(string what) =>
        new($"the swap-readiness fixture serializer has no spelling for {what}.");

    public override void Visit(ILike node) => throw Refused(nameof(ILike));

    public override void Visit(AnyOf node) => throw Refused(nameof(AnyOf));

    public override void Visit(Comparable node) => throw Refused(nameof(Comparable));

    public override void Visit(JsonKeyText node) => throw Refused(nameof(JsonKeyText));

    public override void Visit(JsonKeyExists node) => throw Refused(nameof(JsonKeyExists));

    public override void Visit(JsonArrayContainsString node) => throw Refused(nameof(JsonArrayContainsString));

    public override void Visit(JsonArrayContainsAny node) => throw Refused(nameof(JsonArrayContainsAny));

    public override void Visit(OptionRank node) => throw Refused(nameof(OptionRank));

    public override void Visit(MovedNumberOrEpochMillis node) => throw Refused(nameof(MovedNumberOrEpochMillis));

    public override void Visit(ClockPlusMinutes node) => throw Refused(nameof(ClockPlusMinutes));

    public override void Visit(NamedLock node) => throw Refused(nameof(NamedLock));

    public override void Visit(JsonDocumentOrEmpty node) => throw Refused(nameof(JsonDocumentOrEmpty));

    public override void Visit(JsonConcat node) => throw Refused(nameof(JsonConcat));

    public override void Visit(JsonRemoveKey node) => throw Refused(nameof(JsonRemoveKey));

    public override void Visit(JsonObjectOf node) => throw Refused(nameof(JsonObjectOf));

    public override void Visit(JsonIntOrZero node) => throw Refused(nameof(JsonIntOrZero));

    public override void Visit(HistoryChangedKeys node) => throw Refused(nameof(HistoryChangedKeys));

    public override void Visit(HistoryStateQuery node) => throw Refused(nameof(HistoryStateQuery));

    public override void Visit(HistoryRevertUpdate node) => throw Refused(nameof(HistoryRevertUpdate));

    public override void Visit(ExistingTablesQuery node) => throw Refused(nameof(ExistingTablesQuery));

    public override void Visit(ConstraintColumnsOf node) => throw Refused(nameof(ConstraintColumnsOf));

    public override void Visit(HistorySoftDeleteLookup node) => throw Refused(nameof(HistorySoftDeleteLookup));
}
