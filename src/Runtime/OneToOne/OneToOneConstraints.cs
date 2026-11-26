namespace Neemle.OrmLite;

public sealed record OneToOneConstraints(string ForeignKeyName, string UniqueName, Nodes.ReferentialAction OnDelete);
