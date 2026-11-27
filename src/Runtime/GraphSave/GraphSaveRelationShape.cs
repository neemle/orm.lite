namespace Neemle.OrmLite;

public sealed record GraphSaveRelationShape(string Navigation, GraphSaveRelationKind Kind,
    GraphSaveForeignKeyOwner ForeignKeyOwner, string? ForeignKeyProperty);
