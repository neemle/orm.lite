using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public readonly record struct IndexTerm(
    string Column,
    bool Descending = false,
    NullsPlacement Nulls = NullsPlacement.Unspecified,
    string? Collation = null,
    bool Lower = false)
{
    public static IndexTerm Of(string column, bool descending, NullsPlacement nulls, string? collation, bool lower) =>
        new(column, descending, nulls, collation, lower);

    public static IndexTerm Asc(string column) => new(column);

    public static IndexTerm Desc(string column) => new(column, true);

    public static IndexTerm LowerOf(string column) => new(column, Lower: true);

    public static implicit operator IndexTerm(string column) => new(column);

    public IndexTerm NullsFirst() => this with { Nulls = NullsPlacement.First };

    public IndexTerm NullsLast() => this with { Nulls = NullsPlacement.Last };

    public IndexTerm Collate(string collation) => this with { Collation = collation };

    public bool IsKey => Collation is null && !Lower;

    public Node ToNode()
    {
        Node operand = new ColumnRef(Column);
        if (Lower) operand = new Call(Fn.Lower, [operand]);
        return Descending || Nulls != NullsPlacement.Unspecified || Collation is not null
            ? new OrderTerm(operand, Descending, Nulls, Collation)
            : operand;
    }

    public string Identity => string.Join(
        ":", Column, Lower ? "lowered" : "plain", Descending ? "desc" : "asc", Nulls, Collation ?? "");
}
