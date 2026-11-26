namespace FoundationProof;

internal static class Check
{
    internal static void That(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
    }

    internal static void Refuses(Action action, Neemle.OrmLite.SchemaDiffError error)
    {
        try { action(); }
        catch (Neemle.OrmLite.SchemaDiffException failure)
        {
            That(failure.Error == error, "wrong category");
            return;
        }
        throw new InvalidOperationException("missing refusal");
    }
}
