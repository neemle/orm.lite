using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaRecipeIdentity
{
    internal static string Target(SchemaDiffSnapshot target)
    {
        var w = new SchemaDiffCanonical(); w.Add("schema-target-v1");
        w.Add(target.SchemaToken); w.Add(target.ModelVersion);
        w.Add(target.Desired.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var table in target.Desired.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            w.Add("table"); w.Add(table.Name);
            w.Add(table.Columns.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var column in table.Columns.OrderBy(c => c.Column, StringComparer.Ordinal))
            {
                w.Add("column"); w.Add(column.Property); w.Add(column.Column); w.Add(column.Type.ToString());
                w.Add(column.Nullable.ToString()); w.Add(column.Key.ToString());
                w.Add(column.Precision.ToString(CultureInfo.InvariantCulture));
                w.Add(column.Scale.ToString(CultureInfo.InvariantCulture));
            }
        }
        return w.Digest();
    }

    internal static string Version(SchemaDiffRecipe recipe)
    {
        var w = new SchemaDiffCanonical(); w.Add("schema-operations-v1");
        w.Add(Target(recipe.Before.Target)); w.Add(recipe.Nodes.Up.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var node in recipe.Nodes.Up) Node(w, node);
        w.Add("down"); w.Add(recipe.Nodes.Down.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var node in recipe.Nodes.Down) Node(w, node);
        return recipe.Manifest.Identity.VersionPrefix + "_" + w.Digest();
    }

    internal static string Review(SchemaDiffRecipe recipe)
    {
        var w = new SchemaDiffCanonical(); w.Add("schema-review-v1"); w.Add(Version(recipe));
        w.Add(recipe.Before.Digest); w.Add(recipe.Before.HistoryDigest);
        w.Add(recipe.After.Digest); w.Add(recipe.After.HistoryDigest);
        w.Add(recipe.Manifest.Identity.CSharpNamespace); w.Add(recipe.Manifest.Identity.CSharpTypeName);
        w.Add("explicit-inverse;down-drops-new-data;backup-required;down-proof-required");
        return w.Digest();
    }

    private static void Node(SchemaDiffCanonical w, Node node)
    {
        w.Add(node.GetType().Name);
        switch (node)
        {
            case CreateTable table:
                w.Add(table.Table); foreach (var column in table.Columns) Node(w, column); break;
            case AddColumn add: w.Add(add.Table); Node(w, add.Column); break;
            case DropTable drop: w.Add(drop.Table); break;
            case DropColumn drop: w.Add(drop.Table); w.Add(drop.Column); break;
            case ColumnDef column:
                w.Add(column.Column); w.Add(column.Nullable.ToString()); w.Add(column.PrimaryKey.ToString());
                Node(w, column.Type); if (column.Default is { } value) Node(w, value); else w.Add(null); break;
            case TypeRef type: w.Add(type.Kind.ToString()); break;
            case IntLit or BoolLit or StringLit or ClockNow: w.Add(node.ToString()); break;
            default: throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        }
    }
}
