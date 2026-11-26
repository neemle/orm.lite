using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaRoles
{
    private const string Sql = Data.IntrinsicRoles;

    internal static async Task<IReadOnlyList<object?[]>> ReadAsync(PostgresSchemaRead read,
        uint relation, CancellationToken ct) => await read.RowsAsync(Sql,
            PostgresSchemaRead.Parameter("relation", relation), read.Request.Target.Limits.MaxCatalogObjects, ct);

    internal static IReadOnlyList<object?[]> Normalize(IReadOnlyList<object?[]> rows,
        IReadOnlyList<object?[]> roles, string table)
    {
        var constraints = new Dictionary<string, object?[]>(StringComparer.Ordinal);
        foreach (var role in roles.Where(r => (string)r[2]! is "p" or "n"))
        {
            var kind = (string)role[2]!; var column = (string)role[3]!;
            constraints[(string)role[1]!] = new object?[] { role[0], role[1],
                Framed(kind == "p" ? "primary-key-v1" : "not-null-v1", column), true };
        }
        return rows.Select(row => Normalize(row, roles, constraints, table)).ToArray();
    }

    private static object?[] Normalize(object?[] row, IReadOnlyList<object?[]> roles,
        IReadOnlyDictionary<string, object?[]> constraints, string table)
    {
        var kind = (string)row[0]!; var name = (string)row[1]!;
        if (kind.StartsWith("constraint:", StringComparison.Ordinal) && constraints.TryGetValue(name, out var stamp))
            return stamp;
        var role = roles.SingleOrDefault(r => (string)r[0]! == kind && (string)r[1]! == name);
        if (role is null) return row;
        var value = (string)role[3]!;
        return (string)role[2]! switch
        {
            "row-type" => Intrinsic("row-type", "row-type", Framed("row-type-v1")),
            "toast" => Intrinsic("toast", "toast-storage", Framed("toast-v1")),
            "default" => Intrinsic("default", value, Framed("default-v1", value)),
            "constraint" when constraints.TryGetValue(value, out var constraint) =>
                new object?[] { "dependent-local:constraint", value, constraint[2], true },
            "index" =>
                new object?[] { "index:pk", name, Framed("primary-index-v1", value), true },
            _ => row,
        };
    }

    private static object?[] Intrinsic(string kind, string name, string definition) =>
        new object?[] { "dependent-local:" + kind, name, definition };

    private static string Framed(params string[] values) => string.Concat(values.Select(value =>
        value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value));
}
