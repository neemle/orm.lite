namespace Neemle.OrmLite;

internal sealed record OrphanPresentWrite<T>(T Child, OrphanPresentIntent Intent) where T : class;
