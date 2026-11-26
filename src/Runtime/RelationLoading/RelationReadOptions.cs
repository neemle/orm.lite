namespace Neemle.OrmLite;

public sealed record RelationReadOptions(EagerBehavior Eager = EagerBehavior.UseModel, LoadingScope? Scope = null);
