namespace Neemle.OrmLite;

/// <summary>
/// A non-diagnosed session query answering one value, and the statement text rendered from that value.
/// </summary>
internal sealed record MaintenanceLookup(string Text, Func<string?, string> Render);
