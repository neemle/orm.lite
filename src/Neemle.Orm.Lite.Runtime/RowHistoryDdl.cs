#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public sealed record RowHistoryTable(string Table, string KeyColumn, string SoftColumn, string TenantColumn, string[] Unversioned);

public static class RowHistoryDdl
{
    public const string TableName = "row_history";

    public const string VersionPrefix = "9900_orm_history_reconcile_";

    public const string FunctionName = "row_history_capture";

    public const string UpdateTrigger = "row_history_u";

    public const string DeleteTrigger = "row_history_d";

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    public static string[] Preamble => PreambleFor(OrmLiteSettings.ActorSetting);

    public static Node Install(IReadOnlyList<RowHistoryTable> tables) => InstallFor(OrmLiteSettings.ActorSetting, tables);

    internal static string[] PreambleFor(string actorSetting) => ReconcileFor(actorSetting, []);

    public static string[] Reconcile(IReadOnlyList<RowHistoryTable> tables) =>
        ReconcileFor(OrmLiteSettings.ActorSetting, tables);

    internal static string[] ReconcileFor(string actorSetting, IReadOnlyList<RowHistoryTable> tables) =>
        Render(InstallFor(actorSetting, tables));

    public static string SetHash(IReadOnlyList<RowHistoryTable> tables) =>
        SetHashWith(ProviderRegistry.Find(ProviderRegistry.PostgresId), tables);

    internal static string SetHashWith(ISqlProvider? legacy, IReadOnlyList<RowHistoryTable> tables) =>
        legacy is null
            ? StatementsHash(tables.Select(t => t.Table + "|" + ArgumentsHash(t)))
            : StatementsHash(DdlCompiler.Render([InstallFor(OrmLiteSettings.ActorSetting, tables)], legacy));

    public static string StatementsHash(IEnumerable<string> statements) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', statements))))[..16];

    public static string ArgumentsHash(RowHistoryTable table) =>
        StatementsHash([table.KeyColumn, table.SoftColumn, table.TenantColumn, .. table.Unversioned]);

    internal static string FunctionSqlFor(string actorSetting)
    {
        OrmLiteSettings.Validate(actorSetting);
        return Render(new RowHistoryFunction(actorSetting))[0];
    }

    private static Node InstallFor(string actorSetting, IReadOnlyList<RowHistoryTable> tables)
    {
        OrmLiteSettings.Validate(actorSetting);
        return new RowHistoryInstall(actorSetting, NodeList.Of(tables.Select(Attach)));
    }

    private static string[] Render(Node op) =>
        DdlCompiler.Compile(op, ProviderRegistry.Require(ProviderRegistry.PostgresId)).Select(s => s.Text).ToArray();

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
