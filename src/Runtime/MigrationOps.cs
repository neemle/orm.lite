using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public enum ReversibilityFinding
{
    None,
    DeclaredReversibleButNotInvertible,
    DeclaredIrreversibleButInvertible,
}

public static class MigrationOps
{
    public static OpClass ClassOf(Node op) => op switch
    {
        CreateTable or AddColumn or CreateIndex or RenameColumn or AddForeignKey
            or ConvertForeignKeyAction or ConvertToLiveOnlyIndex or SoftDeleteColumn or CreateView { Replace: false } => OpClass.Reversible,
        DropTable or DropColumn or DropIndex or DropForeignKey or DeleteData or DropView => OpClass.Lossy,
        _ => OpClass.Unknown,
    };

    public static Node? Inverse(Node op) => op switch
    {
        CreateTable n => new DropTable(n.Table),
        CreateView { Replace: false } n => new DropView(n.View),
        AddColumn n => new DropColumn(n.Table, n.Column.Column),
        CreateIndex n => new DropIndex(n.Index),
        RenameColumn n => n with { From = n.To, To = n.From },
        AddForeignKey n => new DropForeignKey(n.Constraint, n.Table),
        ConvertForeignKeyAction n => n with { Target = n.Restore, Restore = n.Target },
        ConvertToLiveOnlyIndex n => n with { Revert = !n.Revert },
        SoftDeleteColumn n => n with { Remove = !n.Remove },
        _ => null,
    };

    public static IReadOnlyList<Node>? DeriveDown(IReadOnlyList<Node> up)
    {
        ArgumentNullException.ThrowIfNull(up);
        var down = new List<Node>(up.Count);
        for (var i = up.Count - 1; i >= 0; i--)
        {
            if (ClassOf(up[i]) != OpClass.Reversible || Inverse(up[i]) is not { } inverse) return null;
            down.Add(inverse);
        }
        return down;
    }

    public static ReversibilityFinding Audit(MigrationContribution contribution, ISqlProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        contribution.Require();
        return contribution.Nodes is { } nodes
            ? Audit(nodes(provider), contribution.Reversibility, contribution.Down is not null)
            : ReversibilityFinding.None;
    }

    public static ReversibilityFinding Audit(IReadOnlyList<Node> up, Reversibility declared, bool hasDownOverride)
    {
        ArgumentNullException.ThrowIfNull(up);
        var invertible = DeriveDown(up) is not null;
        return declared switch
        {
            Reversibility.Reversible when !hasDownOverride && !invertible => ReversibilityFinding.DeclaredReversibleButNotInvertible,
            Reversibility.Irreversible when invertible && up.Count > 0 => ReversibilityFinding.DeclaredIrreversibleButInvertible,
            _ => ReversibilityFinding.None,
        };
    }
}
