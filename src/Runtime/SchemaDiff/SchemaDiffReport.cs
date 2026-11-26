namespace Neemle.OrmLite;

public sealed class SchemaDiffReport
{
    internal SchemaDiffReport(SchemaDiffStatus status, IReadOnlyList<SchemaDiffChange> changes,
        IReadOnlyList<string> diagnostics, SchemaDiffComparison comparison)
    {
        Status = status;
        Changes = Array.AsReadOnly(changes.OrderBy(c => c.Object.Table, StringComparer.Ordinal)
            .ThenBy(c => c.Object.Column, StringComparer.Ordinal).ThenBy(c => c.Kind).ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.Order(StringComparer.Ordinal).Distinct().ToArray());
        Comparison = comparison;
    }

    public SchemaDiffStatus Status { get; }
    public IReadOnlyList<SchemaDiffChange> Changes { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    internal SchemaDiffComparison Comparison { get; }
}
