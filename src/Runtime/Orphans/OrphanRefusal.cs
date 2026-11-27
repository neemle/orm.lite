namespace Neemle.OrmLite;

public static class OrphanRefusal
{
    public static OrphanException At(OrphanError error) => new(error, OrphanOutcome.NotStarted);
}
