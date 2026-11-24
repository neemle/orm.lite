#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed record RowHistoryTable(string Table, string KeyColumn, string SoftColumn, string TenantColumn, string[] Unversioned);

public static class RowHistoryDdl
{
    public const string TableName = "row_history";

    public const string VersionPrefix = "9900_orm_history_reconcile_";

    public const string FunctionName = "row_history_capture";

    public const string UpdateTrigger = "row_history_u";

    public const string DeleteTrigger = "row_history_d";

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    public static string[] Preamble(ISqlProvider provider) => PreambleFor(provider, OrmLiteSettings.ActorSetting);

    public static Node Install(IReadOnlyList<RowHistoryTable> tables) => InstallFor(OrmLiteSettings.ActorSetting, tables);

    internal static string[] PreambleFor(ISqlProvider provider, string actorSetting) =>
        ReconcileFor(provider, actorSetting, []);

    public static string[] Reconcile(ISqlProvider provider, IReadOnlyList<RowHistoryTable> tables) =>
        ReconcileFor(provider, OrmLiteSettings.ActorSetting, tables);

    internal static string[] ReconcileFor(
        ISqlProvider provider, string actorSetting, IReadOnlyList<RowHistoryTable> tables) =>
        Render(provider, InstallFor(actorSetting, tables));

    // The identity of a history set: the table names and the hash of each table's arguments, never rendered text, so
    // the version name of an install is the same whichever providers are loaded. The capture function names the
    // actor setting, so a setting other than the library default is part of the identity and a changed setting
    // installs the function again; under the default setting the identity is the tables alone.
    public static string SetHash(IReadOnlyList<RowHistoryTable> tables) =>
        SetHash(OrmLiteSettings.ActorSetting, tables);

    public static string SetHash(string actorSetting, IReadOnlyList<RowHistoryTable> tables)
    {
        OrmLiteSettings.Validate(actorSetting);
        var identity = tables.Select(t => t.Table + "|" + ArgumentsHash(t));
        return StatementsHash(
            actorSetting == OrmLiteSettings.DefaultActorSetting ? identity : identity.Prepend("actor=" + actorSetting));
    }

    public static string StatementsHash(IEnumerable<string> statements) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', statements))))[..16];

    public static string ArgumentsHash(RowHistoryTable table) =>
        StatementsHash([table.KeyColumn, table.SoftColumn, table.TenantColumn, .. table.Unversioned]);

    internal static string FunctionSqlFor(ISqlProvider provider, string actorSetting)
    {
        OrmLiteSettings.Validate(actorSetting);
        return Render(provider, new RowHistoryFunction(actorSetting))[0];
    }

    private static Node InstallFor(string actorSetting, IReadOnlyList<RowHistoryTable> tables)
    {
        OrmLiteSettings.Validate(actorSetting);
        return new RowHistoryInstall(actorSetting, NodeList.Of(tables.Select(Attach)));
    }

    private static string[] Render(ISqlProvider provider, Node op)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return DdlCompiler.Compile(op, provider).Select(s => s.Text).ToArray();
    }

    private static Node Attach(RowHistoryTable t)
    {
        Check(t);
        return new HistoryTableNode(t.Table, t.KeyColumn, t.SoftColumn, t.TenantColumn, DdlLiterals.Columns(t.Unversioned), ArgumentsHash(t));
    }

    private static void Check(RowHistoryTable t)
    {
        foreach (var name in new[] { t.Table, t.KeyColumn, t.SoftColumn }.Concat(t.Unversioned))
            if (!Identifier.IsMatch(name))
                throw new ArgumentException("not an identifier: " + name, nameof(t));
        if (t.TenantColumn.Length > 0 && !Identifier.IsMatch(t.TenantColumn))
            throw new ArgumentException("not an identifier: " + t.TenantColumn, nameof(t));
    }
}
