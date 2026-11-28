namespace Neemle.OrmLite;

internal static class JoinResourceCapture
{
    internal static void Validate(JoinInput input)
    {
        if (input.Borrowed is not { } borrowed) return;
        try { StreamResourceChecks.Validate(new(input.Database.DataSource, input.Database.Provider), borrowed); }
        catch (Exception error) { throw new JoinReadException(JoinError.ResourceMismatch, error); }
    }
}
