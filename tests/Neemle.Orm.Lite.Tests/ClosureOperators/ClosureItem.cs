namespace Neemle.Orm.Lite.Tests.ClosureOperators;

public sealed record ClosureItem(
    Guid Id,
    string Title,
    int Rank,
    int? Level,
    int Stars,
    long Views,
    double Score,
    decimal Price,
    bool Active,
    FixtureStatus Status,
    FixtureStatus? Review,
    Guid? Owner);
