#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class ForeignKeyActionDdl
{
    public const string Cascade = "CASCADE";

    public const string SetNull = "SET NULL";

    public const string NoAction = "NO ACTION";

    public const string Restrict = "RESTRICT";

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant);

    public static Node Up(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string newAction, string oldAction, string onUpdate = NoAction)
    {
        Check(table, constraint, columns, referencedTable, referencedColumns, newAction, oldAction, onUpdate);
        return Convert(table, constraint, columns, referencedTable, referencedColumns, newAction, oldAction, onUpdate);
    }

    public static Node Down(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string newAction, string oldAction, string onUpdate = NoAction)
    {
        Check(table, constraint, columns, referencedTable, referencedColumns, newAction, oldAction, onUpdate);
        return Convert(table, constraint, columns, referencedTable, referencedColumns, oldAction, newAction, onUpdate);
    }

    public static ReferentialAction Parse(string action) => action switch
    {
        Cascade => ReferentialAction.Cascade,
        SetNull => ReferentialAction.SetNull,
        NoAction => ReferentialAction.NoAction,
        Restrict => ReferentialAction.Restrict,
        _ => throw new ArgumentException("not a referential action: " + action, nameof(action)),
    };

    private static Node Convert(
        string table, string constraint, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns,
        string target, string restore, string onUpdate) =>
        new ConvertForeignKeyAction(
            table, constraint, DdlLiterals.Columns(columns), referencedTable, DdlLiterals.Columns(referencedColumns),
            Parse(target), Parse(restore), Parse(onUpdate));

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
