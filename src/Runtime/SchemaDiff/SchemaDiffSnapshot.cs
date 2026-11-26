using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class SchemaDiffSnapshot
{
    private SchemaDiffSnapshot(SchemaDiffTarget target, IReadOnlyList<string> tables,
        IReadOnlyList<Node> baseline, IReadOnlyList<SchemaDesiredTable> desired)
    {
        ModelVersion = SchemaDiffNames.Required(target.ModelVersion);
        SchemaToken = SchemaDiffNames.Required(target.Schema.Token);
        Scope = new SchemaDiffScope(SchemaDiffNames.Required(target.Scope.DatabaseScopeId),
            SchemaDiffNames.Required(target.Scope.Namespace), tables);
        Limits = target.Limits with { };
        Baseline = baseline;
        Desired = desired;
    }

    internal string ModelVersion { get; }
    internal string SchemaToken { get; }
    internal SchemaDiffScope Scope { get; }
    internal SchemaDiffLimits Limits { get; }
    internal IReadOnlyList<Node> Baseline { get; }
    internal IReadOnlyList<SchemaDesiredTable> Desired { get; }

    internal static SchemaDiffSnapshot Capture(SchemaDiffTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(target.Schema);
        ArgumentNullException.ThrowIfNull(target.Scope);
        SchemaDiffBudget.Validate(target.Limits);
        var tables = SchemaDiffNames.Limited(target.Scope.OwnedTables, target.Limits.MaxTables);
        SchemaDiffNames.Unique(tables);
        if (tables.Count == 0) throw new SchemaDiffException(SchemaDiffError.Ownership);
        var entities = SchemaDiffNames.Limited(target.Schema.Entities.Where(e => e.Managed), target.Limits.MaxTables);
        var budget = new SchemaDiffBudget(target.Limits);
        foreach (var column in entities.SelectMany(e => e.Columns)) budget.Column();
        var managed = entities.Select(e => e.Table).Order(StringComparer.Ordinal);
        if (!managed.SequenceEqual(tables.Order(StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        var baseline = Array.AsReadOnly(target.Schema.Baseline().ToArray());
        var desired = Array.AsReadOnly(entities.Select(e => new SchemaDesiredTable(e.Table,
            Array.AsReadOnly(e.Columns.Select(c => c with { }).ToArray()), e.IsView,
            e.Indexes.Count > 0 || e.Soft is not null || e.ManyToMany.Count > 0)).ToArray());
        return new SchemaDiffSnapshot(target, tables, baseline, desired);
    }
}
