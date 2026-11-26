using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed record LinkForeignKeyDeclaration(string Name, ReferentialAction OnDelete);
