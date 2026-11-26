using System.Security.Cryptography;
using System.Text;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresSerializer
{
    private string? _schemaDiffTable;

    public SerializedStatement SerializeStrictCreate(CreateTable operation) =>
        new PostgresSerializer { _schemaDiffTable = operation.Table }.StrictCreateCore(operation);

    public override void Visit(ColumnDef node)
    {
        if (_schemaDiffTable is null) { base.Visit(node); return; }
        W.Ident(node.Column); Write(node.Type);
        if (!node.Nullable || node.PrimaryKey)
            W.Ddl(DdlKw.Constraint).Ident(StrictName(_schemaDiffTable, node.Column, "nn")).Kw(Kw.Not).Kw(Kw.Null);
        if (node.PrimaryKey)
            W.Ddl(DdlKw.Constraint).Ident(StrictName(_schemaDiffTable, node.Column, "pk"))
                .Ddl(DdlKw.Primary).Kw(Kw.Key);
        if (node.Default is not null) { W.Ddl(DdlKw.Default); Write(node.Default); }
    }

    internal static string StrictName(string table, string column, string role) => "__orml_sd_" + role + "_"
        + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(table + "\0" + column + "\0" + role)))[..40];
}
