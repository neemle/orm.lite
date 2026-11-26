using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaNodeCopies
{
    internal static IReadOnlyList<Node> Up(IReadOnlyList<Node> nodes, int limit) =>
        Array.AsReadOnly(SchemaDiffNames.Limited(nodes, limit).Select(Operation).ToArray());

    private static Node Operation(Node node) => node switch
    {
        CreateTable { Constraints: null or { Count: 0 } } table => new CreateTable(
            SchemaDiffNames.Required(table.Table), NodeList.Of(table.Columns.Select(Column))),
        AddColumn { IfNotExists: false, Column: { Nullable: true, PrimaryKey: false, Default: null } } add =>
            new AddColumn(SchemaDiffNames.Required(add.Table), Column(add.Column), false),
        _ => throw new SchemaDiffException(SchemaDiffError.InvalidArtifact),
    };

    private static ColumnDef Column(Node node)
    {
        if (node is not ColumnDef { Type: TypeRef type } column || !Enum.IsDefined(type.Kind)
            || type.Kind is ColumnType.Json or ColumnType.Decimal || type.Precision != 0 || type.Scale != 0)
            throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        return new ColumnDef(SchemaDiffNames.Required(column.Column), type with { }, column.Nullable,
            column.PrimaryKey, Default(column.Default));
    }

    private static Node? Default(Node? node) => node switch
    {
        null => null, IntLit { Value: 0 } value => value with { }, BoolLit { Value: false } value => value with { },
        StringLit { Value.Length: 0 } value => value with { }, ClockNow => new ClockNow(),
        _ => throw new SchemaDiffException(SchemaDiffError.InvalidArtifact),
    };

    internal static IReadOnlyList<Node> Down(IReadOnlyList<Node> up) =>
        Array.AsReadOnly((MigrationOps.DeriveDown(up) ?? throw new SchemaDiffException(SchemaDiffError.InvalidArtifact))
            .Select(n => n is DropColumn drop ? drop with { IfExists = false } : n).ToArray());
}
