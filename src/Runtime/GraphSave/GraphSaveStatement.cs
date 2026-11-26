namespace Neemle.OrmLite;

internal sealed record GraphSaveStatement(Statement Statement, GraphSaveError? Missing, bool Anchor, bool Link);
