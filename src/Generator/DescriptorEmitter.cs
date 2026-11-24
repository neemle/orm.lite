using System.Collections.Generic;
using System.Linq;
using System.Text;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Model;
using Neemle.CSharpComposer.Rendering;
using CondDef = Neemle.OrmLite.OrmGenerator.CondDef;
using Entity = Neemle.OrmLite.OrmGenerator.Entity;
using IndexDef = Neemle.OrmLite.OrmGenerator.IndexDef;
using Prop = Neemle.OrmLite.OrmGenerator.Prop;
using SoftDeclaration = Neemle.OrmLite.OrmGenerator.SoftDeclaration;

namespace Neemle.OrmLite;

/// <summary>
/// Emits the expression that builds the EntityDescriptor of an entity: its table, columns, indexes, links and the
/// soft-delete declaration. The runtime reads the descriptor to build every statement, so nothing here is a SQL text.
/// </summary>
internal static class DescriptorEmitter
{
    internal static CsExpression Describe(Entity e, string full, HashSet<string> allTables)
    {
        var d = Syn.Static("EntityDescriptor", "Of").Of(Syn.Str(full), Cs.Id("Table"), Syn.Flag(e.Managed));
        if (e.IsView) d = d.Dot("View").Of();
        d = Columns(d, e);
        d = Indexes(d, e);
        d = Links(d, e, allTables);
        return SoftDelete(d, e);
    }

    private static CsExpression Columns(CsExpression d, Entity e)
    {
        foreach (var p in e.Props)
            d = d.Dot("Property").Of(p.Precision == 0
                ? [Syn.Str(p.Name), Syn.Str(p.Column), ColumnType(p), Syn.Flag(p.Nullable), Syn.Flag(p.IsKey)]
                : [
                    Syn.Str(p.Name), Syn.Str(p.Column), ColumnType(p), Syn.Flag(p.Nullable), Syn.Flag(p.IsKey),
                    Cs.Literal(p.Precision), Cs.Literal(p.Scale),
                ]);
        return d;
    }

    private static CsExpression ColumnType(Prop p) => Cs.Id("Nodes").Dot("ColumnType").Dot(p.Type);

    private static CsExpression Indexes(CsExpression d, Entity e)
    {
        foreach (var ix in e.Indexes.Where(i => i.Terms.Length > 0))
            d = ix.IsPlain
                ? d.Dot("Index").Of(
                    [Syn.Str(ix.Name), Syn.Flag(ix.Unique), Syn.Flag(ix.LiveOnly), .. Syn.Strs(ix.Columns)])
                : DescribeIndex(d, ix);
        return d;
    }

    private static CsExpression Links(CsExpression d, Entity e, HashSet<string> allTables)
    {
        foreach (var mm in e.ManyToMany)
            d = d.Dot("Link").Of(
                Syn.Str(mm.Nav), Syn.Str(mm.JoinTable), Syn.Str(mm.ThisKey), Syn.Str(mm.OtherKey),
                Syn.Str(mm.OtherTable), Syn.Str(mm.OtherKeyCol),
                mm.OtherSoftColumn is null ? Syn.Null : Syn.Str(mm.OtherSoftColumn),
                Syn.Flag(!allTables.Contains(mm.JoinTable)));
        return d;
    }

    private static CsExpression SoftDelete(CsExpression d, Entity e)
    {
        if (e.SoftDeleteColumn is null || e.Soft is not { } soft) return d;
        var column = e.Props.First(p => p.Name == e.SoftDeleteColumn).Column;
        d = d.Dot("SoftDelete").Of(Syn.Str(e.SoftDeleteColumn), Syn.Str(column), Syn.Str(EntityToken(e.Type)));
        return SoftOptions(d, soft);
    }

    private static CsExpression SoftOptions(CsExpression d, SoftDeclaration soft)
    {
        if (!soft.Platform && soft.TenantProp is { } tenant) d = d.Dot("Tenant").Of(Syn.Str(tenant.Column));
        if (soft.LabelProp is { Read: "String" } label) d = d.Dot("Label").Of(Syn.Str(label.Column));
        if (soft.UnversionedColumns.Length > 0) d = d.Dot("Unversioned").Of(Syn.Strs(soft.UnversionedColumns));
        if (soft.OwnerColumns.Length > 0) d = d.Dot("Owners").Of(Syn.Strs(soft.OwnerColumns));
        if (soft.PinnedColumns.Length > 0) d = d.Dot("Pinned").Of(Syn.Strs(soft.PinnedColumns));
        if (soft.StatusColumns.Length > 0) d = d.Dot("StatusColumns").Of(Syn.Strs(soft.StatusColumns));
        return d;
    }

    private static CsExpression DescribeIndex(CsExpression d, IndexDef ix)
    {
        var terms = ix.Terms.Select(t => Syn.Static("IndexTerm", "Of").Of(
            Syn.Str(t.Column),
            Syn.Flag(t.Descending),
            Cs.Id("Nodes").Dot("NullsPlacement").Dot(NullsName(t.Nulls)),
            t.Collation is null ? Syn.Null : Syn.Str(t.Collation),
            Syn.Flag(t.Lower)));
        d = d.Dot("Index").Of([Syn.Str(ix.Name), Syn.Flag(ix.Unique), Syn.Flag(ix.LiveOnly), .. terms]);
        return ix.Filter.IsEmpty ? d : d.Dot("Filter").Of([.. ix.Filter.Select(ConditionOf)]);
    }

    private static string NullsName(int nulls) =>
        nulls == IndexTerms.NullsFirst ? "First" : nulls == IndexTerms.NullsLast ? "Last" : "Unspecified";

    private static CsExpression ConditionOf(CondDef c) => c.Kind switch
    {
        "" when c.Op == "IsNull" => Syn.Static("IndexCondition", "Null").Of(Syn.Str(c.Column)),
        "" => Syn.Static("IndexCondition", "NotNull").Of(Syn.Str(c.Column)),
        _ => Syn.Static("IndexCondition", "Of").Of(Syn.Str(c.Column), Syn.Static("IndexFilterOp", c.Op), ValueOf(c)),
    };

    private static CsExpression ValueOf(CondDef c) => c.Kind switch
    {
        "string" => Syn.Str(c.Value),
        "bool" => Syn.Flag(c.Value == "true"),
        _ => Cs.Literal(long.Parse(c.Value, System.Globalization.CultureInfo.InvariantCulture)),
    };

    /// <summary>The snake case token of a type name, which names the history objects of a soft-delete entity.</summary>
    private static string EntityToken(string typeName)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < typeName.Length; i++)
        {
            var ch = typeName[i];
            if (i > 0 && IsUpper(ch) && IsLowerOrDigit(typeName[i - 1])) sb.Append('_');
            sb.Append(IsUpper(ch) ? (char)(ch + 32) : ch);
        }
        return sb.ToString();
    }

    private static bool IsUpper(char ch) => ch >= 'A' && ch <= 'Z';

    private static bool IsLowerOrDigit(char ch) => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
}
