using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveFaultAssertions
{
    internal static GraphSaveRequest<SaveProbeNode> Request(out SaveProbeNode root, out SaveProbeNode child)
    {
        SaveProbeModel.Register();
        root = SaveProbeModel.Node(401); child = SaveProbeModel.Node(402); root.Children.Add(child);
        return new(root, GraphSaveIntent.Insert, new(GraphSaveIntents.Empty.With(child, GraphSaveIntent.Insert),
            SaveProbeModel.ChildInserts, SaveProbeModel.Room));
    }
    internal static void Cleaned(SaveFaultProbe probe)
    {
        Assert.Equal(1, probe.Opens); Assert.Equal(1, probe.Closes);
        Assert.Equal(probe.Executions, probe.CommandDisposals);
        Assert.Equal(1, probe.TransactionDisposals); Assert.Equal(1, probe.ConnectionDisposals); Assert.Equal(1, probe.Releases);
    }
    internal static IReadOnlyCollection<Exception> OriginalErrors(Exception root)
    {
        var table = (ConditionalWeakTable<Exception, PolicySecondaryErrors>)typeof(PolicyCleanup)
            .GetField("Secondary", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var field = typeof(PolicySecondaryErrors).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        void Visit(Exception current)
        {
            if (!seen.Add(current)) return;
            if (current.InnerException is { } inner) Visit(inner);
            if (table.TryGetValue(current, out var retained))
                foreach (var secondary in (List<Exception>)field.GetValue(retained)!) Visit(secondary);
        }
        Visit(root);
        return seen;
    }
}
