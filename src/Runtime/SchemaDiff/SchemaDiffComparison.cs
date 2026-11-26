using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed record SchemaDiffComparison(
    SchemaDiffSnapshot Target, SchemaDiffObservation Before, IReadOnlyList<Node> Up);
