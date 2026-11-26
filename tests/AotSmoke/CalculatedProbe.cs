using System.Data;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Postgres;

namespace AotSmoke;

internal static class CalculatedProbe
{
    internal static bool Run()
    {
        var db = new OrmContext(new IdleSource(), PostgresProvider.Instance);
        var sql = AotCalculatedItemOrm.Query(db).RenderSelect();
        if (!sql.Contains("lower(\"label\") AS \"lower_label\"", StringComparison.Ordinal)) return false;
        using var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("label", typeof(string));
        table.Columns.Add("lower_label", typeof(string));
        table.Rows.Add(Guid.Empty, "MiXeD", "mixed");
        using var reader = table.CreateDataReader();
        if (!reader.Read()) return false;
        var row = AotCalculatedItemOrm.Map(reader);
        return row.Label == "MiXeD" && row.LowerLabel == "mixed"
            && AotCalculatedItemOrm.Values(row).Length == 2;
    }
}
