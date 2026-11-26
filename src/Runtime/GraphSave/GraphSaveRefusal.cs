namespace Neemle.OrmLite;

internal static class GraphSaveRefusal
{
    internal static GraphSaveException At(GraphSaveError error, Exception? cause = null) =>
        new(error, GraphSaveOutcome.NotStarted, cause);

    internal static void Intent(GraphSaveIntent intent)
    {
        if (!Enum.IsDefined(intent)) throw At(GraphSaveError.InvalidRequest);
    }
}
