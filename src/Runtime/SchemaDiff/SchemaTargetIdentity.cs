namespace Neemle.OrmLite;

internal static class SchemaTargetIdentity
{
    internal static bool Same(SchemaDiffSnapshot left, SchemaDiffSnapshot right)
    {
        if (left.Desired.Count != right.Desired.Count) return false;
        foreach (var table in left.Desired)
        {
            var other = right.Desired.SingleOrDefault(t => t.Name == table.Name);
            if (other is null || table.IsView != other.IsView || table.ComplexEffects != other.ComplexEffects
                || !table.Columns.OrderBy(c => c.Column, StringComparer.Ordinal)
                    .SequenceEqual(other.Columns.OrderBy(c => c.Column, StringComparer.Ordinal))) return false;
        }
        return true;
    }
}
