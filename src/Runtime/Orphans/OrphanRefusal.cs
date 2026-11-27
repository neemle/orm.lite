namespace Neemle.OrmLite;

internal static class OrphanRefusal
{
    internal static OrphanException At(OrphanError error) => new(error, OrphanOutcome.NotStarted);
}
