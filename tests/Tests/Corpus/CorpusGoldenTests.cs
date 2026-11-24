using Neemle.OrmLite.Tests.Settings;
using Xunit;

namespace Neemle.OrmLite.Tests.Corpus;

[Collection(ActorSettingCollection.Name)]
public sealed class CorpusGoldenTests
{
    private const string Refresh = "dotnet run scripts/CorpusCapture.cs -- capture";

    [Fact(DisplayName = "the live PostgreSQL output of the library equals every golden of the corpus, and no golden is orphaned")]
    public void The_live_output_equals_the_goldens()
    {
        var root = RuntimeSources.LibraryRoot();
        var result = CorpusCatalog.Capture(root);

        var problems = CorpusCatalog.Compare(root, result);

        Assert.True(problems.Count == 0,
            problems.Count + " corpus difference(s):\n  " + string.Join("\n  ", problems.Take(40))
            + (problems.Count > 40 ? "\n  ..." : "")
            + "\nIf the change is an approved deviation, refresh with `" + Refresh + "`, review the diff and commit it with the change.");
    }

    [Fact(DisplayName = "every statement-producing member of the surveyed surface is referenced by a case or excluded with a reason")]
    public void Every_surface_member_is_accounted_for()
    {
        var result = CorpusCatalog.Capture(RuntimeSources.LibraryRoot());

        Assert.True(result.Entries.Count > 0);
        Assert.True(CorpusIndex.Unaccounted.Count == 0, "unaccounted: " + string.Join(", ", CorpusIndex.Unaccounted));
    }

    [Fact(DisplayName = "capturing twice in one process yields identical text for every entry and for the index")]
    public void Capture_is_deterministic()
    {
        var root = RuntimeSources.LibraryRoot();

        var first = CorpusCatalog.Capture(root);
        var second = CorpusCatalog.Capture(root);

        Assert.Equal(first.Entries.Select(e => e.Group + "/" + e.Name), second.Entries.Select(e => e.Group + "/" + e.Name));
        var different = first.Entries.Zip(second.Entries).Where(p => p.First.Text != p.Second.Text).Select(p => p.First.Group + "/" + p.First.Name).ToList();
        Assert.True(different.Count == 0, "non-deterministic entries: " + string.Join(", ", different));
        Assert.Equal(first.Index, second.Index);
    }
}
