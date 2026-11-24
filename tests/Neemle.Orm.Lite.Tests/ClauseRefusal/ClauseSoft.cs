namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed record ClauseSoft(Guid Id, string Name, int Rank, decimal Price, DateTime? DeletedAt, Jsonb? Doc);
