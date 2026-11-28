namespace Neemle.OrmLite;

public partial interface IMaintenanceProvider
{
    /// <summary>
    /// Renders the WAL checkpoint statement for exactly <paramref name="mode"/>, never another mode. A provider without
    /// <see cref="Capability.WalCheckpoint"/> is refused before this is called, so it need not implement it.
    /// </summary>
    string RenderWalCheckpoint(WalCheckpointMode mode) =>
        throw new NotSupportedException("this maintenance provider does not render a WAL checkpoint.");
}
