namespace Neemle.OrmLite.Tests.Joins;

internal sealed class JoinUnitRow<TMarker>
{
    internal static int GetterCalls;
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Note { get; set; }
    public int Rank { get; set; }
    public int? OptionalRank { get; set; }
    public bool Enabled { get; set; }
    public DateTime? Stamp { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string Lower { get; set; } = "";
    public int ProbeGetter { get { GetterCalls++; throw new InvalidOperationException("marker getter ran"); } }
}
