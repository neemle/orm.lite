#nullable enable
using System;
using System.Text.RegularExpressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public static class SoftDeleteColumnDdl
{
    public const string Column = "deleted_at";

    private static readonly Regex TableName = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    private static readonly Regex VersionName = new(@"\A[a-z0-9][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    public static Node Up(string table, string version)
    {
        Check(table, version);
        return new SoftDeleteColumn(table, version, false);
    }

    public static Node Down(string table, string version)
    {
        Check(table, version);
        return new SoftDeleteColumn(table, version, true);
    }

    private static void Check(string table, string version)
    {
        if (!TableName.IsMatch(table))
            throw new ArgumentException("not a table name: " + table, nameof(table));
        if (!VersionName.IsMatch(version))
            throw new ArgumentException("not a migration version: " + version, nameof(version));
    }
}
