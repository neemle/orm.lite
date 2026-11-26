using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

internal static class RefusalCases
{
    internal static void Run(Fixture f)
    {
        var before = f.Counter.Count;
        var parent = EntityDescriptor.Of("UnsupportedParent", "unsupported_parents", true)
            .Property("Id", "id", ColumnType.Uuid, false, true);
        var child = EntityDescriptor.Of("UnsupportedChild", "unsupported_children", true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, false, false)
            .ForeignKey(new("fk_unsupported", "parent_id", new("unsupported_parents", "id"), ReferentialAction.SetNull));
        Require(ForeignKeyDeclarationError.NonNullableSetNull, () => OrmSchema.Of("BadNull", child, parent).Baseline());
        var self = EntityDescriptor.Of("Self", "unsupported_self", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .ForeignKey(new("fk_self", "parent_id", new("unsupported_self", "id"), ReferentialAction.NoAction));
        Require(ForeignKeyDeclarationError.DependencyCycle, () => OrmSchema.Of("BadCycle", self).Baseline());
        Results.Require(f.Counter.Count == before, "invalid shape refuses without schema I/O");
        Results.Case("refusals", "NonNullableSetNull|DependencyCycle:zero-SQL");
    }
    private static void Require(ForeignKeyDeclarationError expected, Func<IReadOnlyList<Node>> plan)
    {
        try { plan(); }
        catch (ForeignKeyDeclarationException error) { Results.Require(error.Error == expected, "exact refusal"); return; }
        throw new InvalidOperationException("Expected foreign-key declaration refusal");
    }
}
