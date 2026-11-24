using System.Xml.Linq;

namespace Neemle.OrmLite.Tests.Ports;

// The frozen dependency arrows of the projects, read from the project files. The core assemblies (Runtime, Sql) name
// no package; a provider assembly reaches the core by its project references and its own driver; the pack project
// names the assemblies it carries; the generator names no runtime assembly; the rails project sees the ports alone.
internal static class ProjectArrows
{
    private const string Core = "Core";

    internal static readonly IReadOnlyDictionary<string, string[]> Frozen =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["src/Runtime"] = [],
        ["src/Sql"] = ["Runtime"],
        ["src/Generator"] = [],
        ["src/Core"] = ["Generator", "Runtime", "Sql"],
        ["src/Postgres"] = [Core, "Runtime", "Sql"],
        ["src/Sqlite"] = [Core, "Runtime", "Sql"],
        ["tests/Ports.Tests"] = ["Runtime", "Sql"],
    };

    internal static string[] ReferencesOf(string root, string project) =>
        Includes(root, project, "ProjectReference").Select(i => Path.GetFileNameWithoutExtension(i.Replace('\\', '/')))
            .Order(StringComparer.Ordinal).ToArray();

    internal static string[] PackagesOf(string root, string project) =>
        Includes(root, project, "PackageReference").Order(StringComparer.Ordinal).ToArray();

    internal static IReadOnlyList<string> SourceProjects(string root) =>
        Directory.GetDirectories(Path.Combine(root, "src"))
            .Where(d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj")))
            .Select(d => "src/" + Path.GetFileName(d)).Order(StringComparer.Ordinal).ToList();

    private static IEnumerable<string> Includes(string root, string project, string item)
    {
        var path = Path.Combine(root, project, Path.GetFileName(project) + ".csproj");
        return XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == item)
            .Select(e => (string?)e.Attribute("Include")).OfType<string>().ToList();
    }
}
