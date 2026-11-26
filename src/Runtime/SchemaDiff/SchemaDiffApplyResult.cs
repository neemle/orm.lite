namespace Neemle.OrmLite;

public sealed class SchemaDiffApplyResult
{
    internal SchemaDiffApplyResult(SchemaDiffApplyOutcome outcome, IReadOnlyList<SchemaDiffUnitResult> units,
        IReadOnlyList<string> committed, bool bootstrap, SchemaRestoration restoration)
    {
        Outcome = outcome;
        Units = Array.AsReadOnly(units.ToArray());
        KnownCommittedVersions = Array.AsReadOnly(committed.ToArray());
        BootstrapMayHaveChanged = bootstrap;
        SessionRestoration = restoration;
    }

    public SchemaDiffApplyOutcome Outcome { get; }
    public IReadOnlyList<SchemaDiffUnitResult> Units { get; }
    public IReadOnlyList<string> KnownCommittedVersions { get; }
    public bool BootstrapMayHaveChanged { get; }
    public SchemaRestoration SessionRestoration { get; }
}
