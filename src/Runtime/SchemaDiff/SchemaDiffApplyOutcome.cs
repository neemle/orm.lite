namespace Neemle.OrmLite;

public enum SchemaDiffApplyOutcome
{
    NotStarted, Applied, AlreadyApplied, CoveredByCurrentBaseline, PartiallyApplied, Unknown,
}
