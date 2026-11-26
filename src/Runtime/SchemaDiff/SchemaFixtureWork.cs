using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed record SchemaFixtureWork(
    SchemaFixtureWorkKind Kind, string Version, IReadOnlyList<Node> Operations, Node? Receipt);
