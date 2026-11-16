#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neemle.Orm.Lite;

public static class ForeignKeyActionDdl
{
    public const string Cascade = "CASCADE";

    public const string SetNull = "SET NULL";

    public const string NoAction = "NO ACTION";

    public const string Restrict = "RESTRICT";

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant);

    public static string Up(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string newAction, string oldAction, string onUpdate = NoAction)
    {
        Check(table, constraint, columns, referencedTable, referencedColumns, newAction, oldAction, onUpdate);
        return Alter(table, constraint, columns, referencedTable, referencedColumns, newAction, onUpdate);
    }

    public static string Down(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string newAction, string oldAction, string onUpdate = NoAction)
    {
        Check(table, constraint, columns, referencedTable, referencedColumns, newAction, oldAction, onUpdate);
        return Alter(table, constraint, columns, referencedTable, referencedColumns, oldAction, onUpdate);
    }

    private static string Alter(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string action, string onUpdate)
    {
        return "DO $fk$ DECLARE current_action text; BEGIN"
            + " PERFORM set_config('lock_timeout', '5s', true);"
            + " SELECT c.confdeltype::text INTO current_action FROM pg_constraint c"
            + " WHERE c.contype = 'f' AND c.conname = '" + constraint + "'"
            + " AND c.conrelid = to_regclass('\"" + table + "\"');"
            + " IF current_action IS NOT NULL AND current_action <> '" + Code(action) + "' THEN"
            + " ALTER TABLE \"" + table + "\" DROP CONSTRAINT \"" + constraint + "\","
            + " ADD CONSTRAINT \"" + constraint + "\" FOREIGN KEY (" + Quoted(columns) + ")"
            + " REFERENCES \"" + referencedTable + "\" (" + Quoted(referencedColumns) + ")"
            + " ON UPDATE " + onUpdate + " ON DELETE " + action + ";"
            + " END IF;"
            + " END $fk$";
    }

    public static string Code(string action) => action switch
    {
        Cascade => "c",
        SetNull => "n",
        NoAction => "a",
        Restrict => "r",
        _ => throw new ArgumentException("not a referential action: " + action, nameof(action)),
    };

    private static string Quoted(IReadOnlyList<string> names) =>
        string.Join(", ", names.Select(n => "\"" + n + "\""));

    private static void Check(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string newAction, string oldAction, string onUpdate)
    {
        RequireIdentifier(table, nameof(table));
        RequireIdentifier(constraint, nameof(constraint));
        RequireIdentifier(referencedTable, nameof(referencedTable));
        RequireIdentifiers(columns, nameof(columns));
        RequireIdentifiers(referencedColumns, nameof(referencedColumns));
        if (columns.Count != referencedColumns.Count)
            throw new ArgumentException("a foreign key needs as many referenced columns as columns", nameof(referencedColumns));
        if (newAction is not (Cascade or SetNull))
            throw new ArgumentException("the converted action is CASCADE or SET NULL: " + newAction, nameof(newAction));
        if (oldAction is not (NoAction or Restrict))
            throw new ArgumentException("the restored action is NO ACTION or RESTRICT: " + oldAction, nameof(oldAction));
        if (onUpdate is not (NoAction or Restrict))
            throw new ArgumentException("the update action is NO ACTION or RESTRICT: " + onUpdate, nameof(onUpdate));
    }

    private static void RequireIdentifier(string value, string parameter)
    {
        if (value is null || !Identifier.IsMatch(value))
            throw new ArgumentException("not an identifier: " + value, parameter);
    }

    private static void RequireIdentifiers(IReadOnlyList<string> values, string parameter)
    {
        if (values is null || values.Count == 0)
            throw new ArgumentException("a foreign key needs at least one column", parameter);
        foreach (var value in values) RequireIdentifier(value, parameter);
    }
}
