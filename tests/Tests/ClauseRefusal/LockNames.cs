using Xunit;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

internal static class LockNames
{
    internal const string Label = "ForUpdate or ForNoKeyUpdate";

    private static readonly string[] Paging = ["Take", "Skip", "OrderBy"];

    internal static void Verify(string message, string operation, params string[] paging)
    {
        Assert.Contains(operation, message, StringComparison.Ordinal);
        Assert.Contains(Label, message, StringComparison.Ordinal);
        foreach (var name in Paging)
        {
            if (paging.Contains(name)) Assert.Contains(name, message, StringComparison.Ordinal);
            else Assert.DoesNotContain(name, message, StringComparison.Ordinal);
        }
        var last = paging.Select(name => message.IndexOf(name, StringComparison.Ordinal)).DefaultIfEmpty(-1).Max();
        var position = message.IndexOf(Label, StringComparison.Ordinal);
        Assert.True(position > last, "the lock is named after every paging clause");
    }
}
