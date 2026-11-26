using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaSourceNodes
{
    internal static string Write(SchemaSourceBuilder w, Node node) => node switch
    {
        CreateTable table => w.Call("N.CreateTable", "new N.CreateTable", w.Text(table.Table),
            w.Call("N.NodeList", "N.NodeList.Of", w.List("N.Node", table.Columns.Select(c => Write(w, c))))),
        AddColumn add => w.Call("N.AddColumn", "new N.AddColumn", w.Text(add.Table), Write(w, add.Column), "false"),
        DropTable drop => w.Call("N.DropTable", "new N.DropTable", w.Text(drop.Table)),
        DropColumn drop => w.Call("N.DropColumn", "new N.DropColumn", w.Text(drop.Table), w.Text(drop.Column), "false"),
        ColumnDef column => Column(w, column),
        TypeRef type => w.Call("N.TypeRef", "new N.TypeRef", "N.ColumnType." + type.Kind),
        IntLit => "new N.IntLit(0)", BoolLit => "new N.BoolLit(false)",
        StringLit => "new N.StringLit(\"\")", ClockNow => "new N.ClockNow()",
        _ => throw new SchemaDiffException(SchemaDiffError.InvalidArtifact),
    };

    private static string Column(SchemaSourceBuilder w, ColumnDef column) =>
        w.Call("N.ColumnDef", "new N.ColumnDef", w.Text(column.Column), Write(w, column.Type),
            SchemaSourceTarget.Boolean(column.Nullable), SchemaSourceTarget.Boolean(column.PrimaryKey),
            column.Default is { } value ? Write(w, value) : "null");
}
