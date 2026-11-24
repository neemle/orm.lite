using Neemle.Orm.Lite.Tests.Behaviour;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

internal static class ClauseTargets
{
    internal static readonly ClauseTarget<ClausePlain> Plain =
        new(x => x.Rank, x => x.Rank, x => x.Name == "n", x => x.Rank == 3);

    internal static readonly ClauseTarget<ClauseSoft> Soft =
        new(x => x.Rank, x => x.Rank, x => x.Name == "n", x => x.Rank == 3);

    internal static readonly ClauseTarget<BehaveNote> Note =
        new(n => n.Stars, n => n.Stars, n => n.Stars >= 0, n => n.Stars == 3);

    internal static readonly ClauseTarget<BehaveCounter> Counter =
        new(c => c.N, c => c.N, c => c.N >= 0, c => c.N == 3);

    internal static IClauseTarget Of(string entity) => entity == "soft" ? Soft : Plain;
}
