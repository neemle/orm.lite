using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed record ForeignKeyDescriptor(
    string Name, string LocalColumn, ForeignKeyTarget Target, ReferentialAction OnDelete);
