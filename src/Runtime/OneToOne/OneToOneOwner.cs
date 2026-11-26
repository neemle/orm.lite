namespace Neemle.OrmLite;

public sealed record OneToOneOwner(string Entity, string ForeignKeyProperty, string ForeignKeyColumn);
