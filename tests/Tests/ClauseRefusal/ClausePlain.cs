namespace Neemle.OrmLite.Tests.ClauseRefusal;

public sealed record ClausePlain(Guid Id, string Name, int Rank, decimal Price, Jsonb? Doc);
