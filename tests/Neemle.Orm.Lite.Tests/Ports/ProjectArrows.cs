using System.Xml.Linq;

namespace Neemle.Orm.Lite.Tests.Ports;

// The frozen dependency arrows of the projects, read from the project files. The core assemblies (Runtime, Sql) name
// no package; a provider assembly reaches the core by its project references and its own driver; the pack project
// names the assemblies it carries; the generator names no runtime assembly; the rails project sees the ports alone.
internal static class ProjectArrows
{
    private const string Core = "Neemle.Orm.Lite";

    internal static readonly IReadOnlyDictionary<string, string[]> Frozen =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["src/Neemle.Orm.Lite.Runtime"] = [],
        ["src/Neemle.Orm.Lite.Sql"] = ["Neemle.Orm.Lite.Runtime"],
        ["src/Neemle.Orm.Lite.Generator"] = [],
        ["src/Neemle.Orm.Lite"] = ["Neemle.Orm.Lite.Generator", "Neemle.Orm.Lite.Runtime", "Neemle.Orm.Lite.Sql"],
        ["src/Neemle.Orm.Lite.Postgres"] = [Core, "Neemle.Orm.Lite.Runtime", "Neemle.Orm.Lite.Sql"],
        ["src/Neemle.Orm.Lite.Sqlite"] = [Core, "Neemle.Orm.Lite.Runtime", "Neemle.Orm.Lite.Sql"],
        ["tests/Neemle.Orm.Lite.Ports.Tests"] = ["Neemle.Orm.Lite.Runtime", "Neemle.Orm.Lite.Sql"],
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
