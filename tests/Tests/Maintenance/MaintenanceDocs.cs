namespace Neemle.OrmLite.Tests.Maintenance;

// Reads docs/maintenance.md and one of its level-2 sections.
internal static class MaintenanceDocs
{
    internal static string Page => File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), "docs", "maintenance.md"));

    internal static string Section(string heading)
    {
        var page = Page;
        var start = page.IndexOf("\n## " + heading + "\n", StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("docs/maintenance.md has no section '" + heading + "'");
        var end = page.IndexOf("\n## ", start + 4, StringComparison.Ordinal);
        return end < 0 ? page[start..] : page[start..end];
    }
}
