namespace Neemle.OrmLite.Nodes;

public enum CompareOp
{
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public enum Fn
{
    Count,
    Sum,
    Min,
    Lower,
    Coalesce,
    Max,
}

public enum AggregateFn
{
    CountDistinct,
    SumOrZero,
    Min,
    DecimalSumOrZero,
    DecimalMin,
    Max,
    Avg,
    DecimalMax,
    DecimalAvg,
}

public enum NullsPlacement
{
    Unspecified,
    First,
    Last,
}

public enum RowLockStrength
{
    Update,
    KeyPreserving,
    Share,
}

public enum CastType
{
    Text,
    Boolean,
    Double,
    Decimal,
    Json,
    Uuid,
}

public enum NamedLockKind
{
    Exclusive,
    Shared,
}
