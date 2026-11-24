using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class FinValue
{
    public const string Table = "fin_values";

    public static readonly string[] Columns = ["id", "grp", "v", "at"];

    public Guid Id { get; set; }

    public Guid Grp { get; set; }

    public decimal V { get; set; }

    public DateTime? At { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(Grp) => "grp",
        nameof(V) => "v",
        nameof(At) => "at",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a FinValue member"),
    };

    public static FinValue Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Grp = r.GetGuid(1),
        V = r.GetDecimal(2),
        At = r.IsDBNull(3) ? null : r.GetDateTime(3),
    };
}

public sealed class FinAccount
{
    public const string Table = "fin_accounts";

    public static readonly string[] Columns = ["id", "ledger", "balance"];

    public Guid Id { get; set; }

    public Guid Ledger { get; set; }

    public decimal Balance { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(Ledger) => "ledger",
        nameof(Balance) => "balance",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a FinAccount member"),
    };

    public static FinAccount Map(DbDataReader r) => new() { Id = r.GetGuid(0), Ledger = r.GetGuid(1), Balance = r.GetDecimal(2) };
}

public sealed class FinEntry
{
    public const string Table = "fin_entries";

    public static readonly string[] Columns = ["id", "ledger", "account_id", "amount"];

    public Guid Id { get; set; }

    public Guid Ledger { get; set; }

    public Guid AccountId { get; set; }

    public decimal Amount { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(Ledger) => "ledger",
        nameof(AccountId) => "account_id",
        nameof(Amount) => "amount",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a FinEntry member"),
    };

    public static FinEntry Map(DbDataReader r) => new() { Id = r.GetGuid(0), Ledger = r.GetGuid(1), AccountId = r.GetGuid(2), Amount = r.GetDecimal(3) };
}

internal static class FinWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    internal static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<FinValue>(FinValue.Table, FinValue.Column, FinValue.Columns);
            OrmMeta.Register<FinAccount>(FinAccount.Table, FinAccount.Column, FinAccount.Columns);
            OrmMeta.Register<FinEntry>(FinEntry.Table, FinEntry.Column, FinEntry.Columns);
            _registered = true;
        }
    }
}
