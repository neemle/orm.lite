namespace Neemle.OrmLite;

public sealed class SchemaDiffObservation
{
    private SchemaDiffObservation(SchemaObservationIdentity identity, IReadOnlyList<SchemaObservedTable> tables,
        SchemaObservationHistory history, SchemaDiffSnapshot target)
    {
        Identity = identity with { };
        Target = target;
        Tables = tables;
        History = history;
        Digest = SchemaObservationDigest.Physical(identity, target, tables);
        HistoryDigest = SchemaObservationDigest.History(history);
    }

    public string Digest { get; }
    public string ProviderProfile => Identity.ProviderProfile;
    public SchemaObservationHistory History { get; }
    public string HistoryDigest { get; }
    public IReadOnlyList<SchemaObservedTable> Tables { get; }
    internal SchemaObservationIdentity Identity { get; }
    internal SchemaDiffSnapshot Target { get; }

    public static SchemaDiffObservation FromFacts(SchemaObservationIdentity identity,
        IReadOnlyList<SchemaObservedTable> tables, SchemaObservationHistory history, SchemaDiffInspection request)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(request);
        SchemaDiffNames.Required(identity.DatabaseIdentityDigest);
        if (identity.ProviderProfile != SchemaDiffNames.Required(request.ExpectedProviderProfile))
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
        var target = SchemaDiffSnapshot.Capture(request.Target);
        var budget = new SchemaDiffBudget(target.Limits);
        var copied = SchemaDiffNames.Limited(tables, target.Limits.MaxTables).Select(t =>
            SchemaFactCopies.Table(t, budget)).OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
        SchemaDiffNames.Unique(copied.Select(t => t.Name));
        if (!copied.Select(t => t.Name).SequenceEqual(target.Scope.OwnedTables.Order(StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        return new SchemaDiffObservation(identity, Array.AsReadOnly(copied),
            SchemaFactCopies.History(history, target.Limits.MaxCatalogObjects), target);
    }
}
