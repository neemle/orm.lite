using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

// The orphan-guard extension surface (TL ruling on §803 F4, ORML-113). The provider-neutral guard SQL is internal to
// the Sql assembly and reaches the two adapters through InternalsVisibleTo. The Runtime pieces the adapters need
// stay public, because the Runtime grants InternalsVisibleTo only to the test assembly
// (OrmLiteRuntimeVisibilityRosterTests); each of them carries XML documentation.
public sealed class OrphanGuardSurfaceTests
{
    private static readonly string[] Documented =
    [
        "Neemle.OrmLite.IOrphanGuardLease",
        "Neemle.OrmLite.IOrphanSnapshotGuard",
        "Neemle.OrmLite.OrphanGuardColumn",
        "Neemle.OrmLite.OrphanGuardColumns",
        "Neemle.OrmLite.OrphanGuardFailure",
        "Neemle.OrmLite.OrphanGuardKeys",
        "Neemle.OrmLite.OrphanGuardRequest",
        "Neemle.OrmLite.OrphanGuardSession",
        "Neemle.OrmLite.OrphanGuardShape",
        "Neemle.OrmLite.OrphanRefusal",
    ];

    private static Assembly[] Libraries() =>
    [
        typeof(OrmContext).Assembly,
        typeof(Neemle.OrmLite.Sql.SqlWriter).Assembly,
        typeof(PostgresProvider).Assembly,
        typeof(SqliteProvider).Assembly,
    ];

    [Fact(DisplayName = "the orphan guard SQL builder and its table pair are not exported by any library assembly")]
    public void The_guard_sql_helpers_are_internal()
    {
        var exported = Libraries().SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name is "OrphanGuardSql" or "OrphanGuardTables")
            .Select(t => t.FullName)
            .ToArray();

        Assert.Empty(exported);
    }

    [Fact(DisplayName = "the exported orphan guard types are exactly the documented provider extension set")]
    public void The_exported_guard_types_are_the_documented_set()
    {
        var exported = Libraries().SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Name.StartsWith("OrphanGuard", StringComparison.Ordinal)
                || t.Name is "IOrphanGuardLease" or "IOrphanSnapshotGuard" or "OrphanRefusal")
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Documented, exported);
    }

    [Fact(DisplayName = "every exported orphan guard type and public member has an XML summary")]
    public void Every_guard_piece_has_xml_documentation()
    {
        var runtime = typeof(OrmContext).Assembly;
        var file = Path.ChangeExtension(runtime.Location, ".xml");
        Assert.True(File.Exists(file), "The Runtime XML documentation file is missing next to " + runtime.Location);
        var members = XDocument.Load(file).Descendants("member")
            .Where(m => m.Element("summary") is not null || m.Element("param") is not null)
            .Select(m => (string)m.Attribute("name")!)
            .ToArray();

        var missing = new List<string>();
        foreach (var type in Documented.Select(n => runtime.GetType(n, throwOnError: true)!))
        {
            if (!members.Contains("T:" + type.FullName, StringComparer.Ordinal)) missing.Add("T:" + type.FullName);
            foreach (var member in Members(type))
            {
                var prefix = member.Kind + ":" + type.FullName + "." + member.Name;
                if (!members.Any(m => m == prefix || m.StartsWith(prefix + "(", StringComparison.Ordinal)
                    || m.StartsWith(prefix + "``", StringComparison.Ordinal)))
                    missing.Add(prefix);
            }
        }

        Assert.Empty(missing);
    }

    private static IEnumerable<(char Kind, string Name)> Members(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;
        var positional = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var ctor in type.GetConstructors(flags).Where(c => c.GetParameters().Length > 0 && !Generated(c)))
            yield return ('M', "#ctor");
        foreach (var method in type.GetMethods(flags).Where(m => !m.IsSpecialName && !Generated(m)))
            yield return ('M', method.Name);
        foreach (var property in type.GetProperties(flags).Where(p => !Generated(p) && !positional.Contains(p.Name)))
            yield return ('P', property.Name);
    }

    private static bool Generated(MemberInfo member) => member.IsDefined(typeof(CompilerGeneratedAttribute));
}
