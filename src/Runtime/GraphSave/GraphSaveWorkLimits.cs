namespace Neemle.OrmLite;

public sealed record GraphSaveWorkLimits(int MaxEntityWrites, int MaxLinkAdds, int MaxStatements);
