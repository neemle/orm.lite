#nullable enable
using System;
using System.Text.RegularExpressions;

namespace Neemle.Orm.Lite;

public static class SoftDeleteColumnDdl
{
    public const string Column = "deleted_at";

    private static readonly Regex TableName = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    private static readonly Regex VersionName = new(@"\A[a-z0-9][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    public static string Up(string table, string version)
    {
        Check(table, version);
        return "DO $sd$ BEGIN"
            + " PERFORM set_config('lock_timeout', '5s', true);"
            + " IF NOT " + ColumnExists(table) + " THEN"
            + " ALTER TABLE \"" + table + "\" ADD COLUMN \"" + Column + "\" timestamp with time zone NULL;"
            + " COMMENT ON COLUMN \"" + table + "\".\"" + Column + "\" IS '" + version + "';"
            + " END IF;"
            + " END $sd$";
    }

    public static string Down(string table, string version)
    {
        Check(table, version);
        return "DO $sd$ DECLARE soft_rows bigint; BEGIN"
            + " PERFORM set_config('lock_timeout', '5s', true);"
            + " IF " + MarkerIs(table, version) + " THEN"
            + " SELECT count(*) INTO soft_rows FROM \"" + table + "\" WHERE \"" + Column + "\" IS NOT NULL;"
            + " IF soft_rows > 0 THEN"
            + " RAISE EXCEPTION 'cannot drop " + Column + " from " + table + ": % soft-deleted row(s) would read as live', soft_rows;"
            + " END IF;"
            + " ALTER TABLE \"" + table + "\" DROP COLUMN \"" + Column + "\";"
            + " END IF;"
            + " END $sd$";
    }

    private static string ColumnExists(string table) =>
        "EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = to_regclass('\"" + table + "\"')"
        + " AND a.attname = '" + Column + "' AND NOT a.attisdropped)";

    private static string MarkerIs(string table, string version) =>
        "coalesce((SELECT col_description(a.attrelid, a.attnum) FROM pg_attribute a"
        + " WHERE a.attrelid = to_regclass('\"" + table + "\"')"
        + " AND a.attname = '" + Column + "' AND NOT a.attisdropped), '') = '" + version + "'";

    private static void Check(string table, string version)
    {
        if (!TableName.IsMatch(table))
            throw new ArgumentException("not a table name: " + table, nameof(table));
        if (!VersionName.IsMatch(version))
            throw new ArgumentException("not a migration version: " + version, nameof(version));
    }
}
