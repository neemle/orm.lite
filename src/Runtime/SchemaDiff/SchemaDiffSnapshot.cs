using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class SchemaDiffSnapshot
{
    private SchemaDiffSnapshot(SchemaDiffTarget target, IReadOnlyList<string> tables, IReadOnlyList<Node> baseline)
    {
        ModelVersion = SchemaDiffNames.Required(target.ModelVersion);
        SchemaToken = SchemaDiffNames.Required(target.Schema.Token);
        Scope = new SchemaDiffScope(SchemaDiffNames.Required(target.Scope.DatabaseScopeId),
            SchemaDiffNames.Required(target.Scope.Namespace), tables);
        Limits = target.Limits with { };
        Baseline = baseline;
    }

    internal string ModelVersion { get; }
    internal string SchemaToken { get; }
    internal SchemaDiffScope Scope { get; }
    internal SchemaDiffLimits Limits { get; }
    internal IReadOnlyList<Node> Baseline { get; }

    internal static SchemaDiffSnapshot Capture(SchemaDiffTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(target.Schema);
        ArgumentNullException.ThrowIfNull(target.Scope);
        SchemaDiffBudget.Validate(target.Limits);
        var tables = SchemaDiffNames.Limited(target.Scope.OwnedTables, target.Limits.MaxTables);
        SchemaDiffNames.Unique(tables);
        if (tables.Count == 0) throw new SchemaDiffException(SchemaDiffError.Ownership);
        var managed = target.Schema.Entities.Where(e => e.Managed).Select(e => e.Table).Order(StringComparer.Ordinal);
        if (!managed.SequenceEqual(tables.Order(StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        var baseline = Array.AsReadOnly(target.Schema.Baseline().ToArray());
        return new SchemaDiffSnapshot(target, tables, baseline);
    }
}
