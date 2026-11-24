using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class TableConstraints
{
    public static TableBuilder ForeignKey(
        this TableBuilder table, string name, string column, string refTable, string refColumn,
        ReferentialAction? onDelete = null, ReferentialAction? onUpdate = null) =>
        table.AddConstraint(new ForeignKeyDef(
            name, DdlLiterals.Columns([column]), refTable, DdlLiterals.Columns([refColumn]), onDelete, onUpdate));
}
