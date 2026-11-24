#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Neemle.Orm.Lite;

public static class OrmMigrationRegistry
{
    private static readonly ConcurrentDictionary<string, Func<IReadOnlyList<MigrationContribution>>> Sources = new(StringComparer.Ordinal);

    public static IReadOnlyList<string> Tokens => Sources.Keys.Order(StringComparer.Ordinal).ToList();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(string token, Func<IReadOnlyList<MigrationContribution>> contributions)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(contributions);
        Sources[token] = contributions;
    }

    public static IReadOnlyList<MigrationContribution> Collect()
    {
        var tokens = Tokens;
        if (tokens.Count == 0)
            throw new InvalidOperationException(
                "no generated migrations are registered. The project that holds the [Table] classes must reference the Neemle.Orm.Lite package, "
                + "which generates and registers them when it is built, and that assembly must have been loaded (touch one of its types before migrating). "
                + "To migrate explicitly, pass the contributions: await db.MigrateAsync(<AssemblyName>Migrations.Contributions).");
        var all = new List<MigrationContribution>();
        foreach (var token in tokens) all.AddRange(Sources[token]());
        var repeated = all.GroupBy(c => c.Version, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
            throw new InvalidOperationException(
                $"migration version '{repeated.Key}' is declared more than once among the registered assemblies ({string.Join(", ", tokens)}); "
                + "a version names one migration, so rename one of them.");
        return all;
    }

    internal static void ResetForTests() => Sources.Clear();
}
