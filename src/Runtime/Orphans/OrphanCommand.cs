namespace Neemle.OrmLite;

internal sealed record OrphanCommand(Statement Statement, OrphanError Missing);
