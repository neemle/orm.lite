using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class SchemaCompareState(SchemaDiffSnapshot target, SchemaDiffObservation observation)
{
    internal SchemaDiffSnapshot Target => target;
    internal SchemaDiffObservation Observation => observation;
    internal List<SchemaDiffChange> Changes { get; } = [];
    internal List<string> Diagnostics { get; } =
        ["physical-schema-only; historical logical-domain intent is not observed"];
    internal List<Node> Up { get; } = [];
    internal bool Incomplete { get; set; }
    internal bool Manual { get; set; }

    internal void Change(SchemaDiffChangeKind kind, SchemaObjectRef item, SchemaDiffImpact impact, string category)
    {
        Changes.Add(new(kind, item, impact, category));
        if (impact is SchemaDiffImpact.Destructive or SchemaDiffImpact.CompatibilityRisk) Manual = true;
        if (impact == SchemaDiffImpact.Unknown) Incomplete = true;
    }

    internal bool Known<T>(SchemaFact<T> fact, SchemaObjectRef item, string category)
    {
        if (fact.Knowledge == SchemaKnowledge.Known) return true;
        Change(SchemaDiffChangeKind.UnknownMetadata, item, fact.Knowledge == SchemaKnowledge.Unknown
            ? SchemaDiffImpact.Unknown : SchemaDiffImpact.CompatibilityRisk, category);
        return false;
    }

    internal void Operation(Node operation)
    {
        if (Up.Count >= target.Limits.MaxOperations)
        {
            Incomplete = true; Diagnostics.Add("operation-limit"); return;
        }
        Up.Add(operation);
    }

    internal SchemaDiffReport Report()
    {
        var status = Incomplete ? SchemaDiffStatus.Incomplete : Manual ? SchemaDiffStatus.ManualRequired
            : Up.Count == 0 ? SchemaDiffStatus.NoOp : SchemaDiffStatus.Changes;
        return new(status, Changes, Diagnostics,
            new(target, observation, Array.AsReadOnly(Up.ToArray())));
    }
}
