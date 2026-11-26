using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

internal sealed record ExpectedKey(string Name, ReferentialAction Action,
    string Local = "parent_id", string Target = "demo_fk_parents");
