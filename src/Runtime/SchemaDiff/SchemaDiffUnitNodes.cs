using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed record SchemaDiffUnitNodes(IReadOnlyList<Node> Up, IReadOnlyList<Node> Down);
