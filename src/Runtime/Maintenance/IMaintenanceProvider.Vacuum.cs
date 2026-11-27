namespace Neemle.OrmLite;

public partial interface IMaintenanceProvider
{
    /// <summary>
    /// Renders VACUUM in exactly <paramref name="mode"/>, of the whole database when <paramref name="table"/> is null
    /// or of that table, quoted by the provider.
    /// </summary>
    string RenderVacuum(VacuumMode mode, EntityDescriptor? table) =>
        throw new NotSupportedException("this maintenance provider does not render VACUUM.");
}
