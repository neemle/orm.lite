using Neemle.OrmLite;
namespace ComposeProbe;
internal static class Check
{
    internal static int Count { get; private set; }
    internal static void That(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException(label);
        Count++;
    }
    internal static async Task Refuse(Func<Task> action, OneToOneError expected)
    {
        try { await action(); }
        catch (OneToOneDeclarationException error)
        { That(error.Error == expected, "exact provenance refusal"); return; }
        throw new InvalidOperationException("Expected provenance refusal");
    }
}
