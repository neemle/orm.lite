using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class VwProject
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Region { get; set; } = "";
}

public sealed class VwItem
{
    public string Id { get; set; } = "";

    public string ProjectId { get; set; } = "";

    public string Title { get; set; } = "";

    public int Points { get; set; }

    public string Status { get; set; } = "";
}

public sealed class VwOpen
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string ProjectName { get; set; } = "";

    public int Points { get; set; }
}

public sealed class VwBig
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
}

public sealed class VwUs
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
}

public sealed class VwTotals
{
    public long Items { get; set; }

    public long Points { get; set; }

    public long Smallest { get; set; }
}

public sealed class VwLine
{
    public string Id { get; set; } = "";

    public decimal Amount { get; set; }
}

public sealed class VwLineView
{
    public string Id { get; set; } = "";

    public decimal Amount { get; set; }
}

public sealed class VwNoteView
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
}

public sealed class VwLineTotals
{
    public decimal Total { get; set; }

    public decimal Smallest { get; set; }
}

internal static class ViewWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    internal static EntityDescriptor Describe<T>(string table, params (string Property, string Column, ColumnType Type)[] columns)
    {
        var descriptor = EntityDescriptor.Of(typeof(T).FullName!, table, false);
        foreach (var (property, column, type) in columns) descriptor.Property(property, column, type, false, false);
        return descriptor;
    }

    internal static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            BehaveWorld.Register();
            OrmMeta.Register<VwProject>(Describe<VwProject>("vw_projects", ("Id", "id", ColumnType.Text), ("Name", "name", ColumnType.Text), ("Region", "region", ColumnType.Text)));
            OrmMeta.Register<VwItem>(Describe<VwItem>(
                "vw_items", ("Id", "id", ColumnType.Text), ("ProjectId", "project_id", ColumnType.Text), ("Title", "title", ColumnType.Text),
                ("Points", "points", ColumnType.Int32), ("Status", "status", ColumnType.Text)));
            OrmMeta.Register<VwOpen>(Describe<VwOpen>(
                "vw_open", ("Id", "id", ColumnType.Text), ("Title", "title", ColumnType.Text), ("ProjectName", "project_name", ColumnType.Text),
                ("Points", "points", ColumnType.Int32)).View());
            OrmMeta.Register<VwBig>(Describe<VwBig>("vw_big", ("Id", "id", ColumnType.Text), ("Title", "title", ColumnType.Text)).View());
            OrmMeta.Register<VwUs>(Describe<VwUs>("vw_us", ("Id", "id", ColumnType.Text), ("Title", "title", ColumnType.Text)).View());
            OrmMeta.Register<VwTotals>(Describe<VwTotals>(
                "vw_totals", ("Items", "items", ColumnType.Int64), ("Points", "points", ColumnType.Int64), ("Smallest", "smallest", ColumnType.Int64)).View());
            OrmMeta.Register<VwLine>(Describe<VwLine>("vw_lines", ("Id", "id", ColumnType.Text), ("Amount", "amount", ColumnType.Decimal)));
            OrmMeta.Register<VwLineView>(Describe<VwLineView>("vw_line_view", ("Id", "id", ColumnType.Text), ("Amount", "amount", ColumnType.Decimal)).View());
            OrmMeta.Register<VwLineTotals>(Describe<VwLineTotals>("vw_line_totals", ("Total", "total", ColumnType.Decimal), ("Smallest", "smallest", ColumnType.Decimal)).View());
            OrmMeta.Register<VwNoteView>(Describe<VwNoteView>("log_note_view", ("Id", "id", ColumnType.Text), ("Title", "title", ColumnType.Text)).View());
            _registered = true;
        }
    }

    internal static string Text(DbDataReader r, string column) => r.GetString(r.GetOrdinal(column));

    internal static int Int(DbDataReader r, string column) => Convert.ToInt32(r.GetValue(r.GetOrdinal(column)));

    internal static long Long(DbDataReader r, string column) => Convert.ToInt64(r.GetValue(r.GetOrdinal(column)));

    internal static VwOpen MapOpen(DbDataReader r) =>
        new() { Id = Text(r, "id"), Title = Text(r, "title"), ProjectName = Text(r, "project_name"), Points = Int(r, "points") };

    internal static VwLineView MapLineView(DbDataReader r) => new() { Id = Text(r, "id"), Amount = r.GetDecimal(r.GetOrdinal("amount")) };

    internal static VwLineTotals MapLineTotals(DbDataReader r) =>
        new() { Total = r.GetDecimal(r.GetOrdinal("total")), Smallest = r.GetDecimal(r.GetOrdinal("smallest")) };

    internal static VwBig MapBig(DbDataReader r) => new() { Id = Text(r, "id"), Title = Text(r, "title") };

    internal static VwUs MapUs(DbDataReader r) => new() { Id = Text(r, "id"), Title = Text(r, "title") };

    internal static VwTotals MapTotals(DbDataReader r) =>
        new() { Items = Long(r, "items"), Points = Long(r, "points"), Smallest = Long(r, "smallest") };
}
