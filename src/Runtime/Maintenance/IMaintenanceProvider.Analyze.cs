namespace Neemle.OrmLite;

public partial interface IMaintenanceProvider
{
    /// <summary>Renders ANALYZE of the whole database, or of <paramref name="table"/> quoted by the provider.</summary>
    string RenderAnalyze(AnalyzeScope scope, EntityDescriptor? table) =>
        throw new NotSupportedException("this maintenance provider does not render ANALYZE.");
}
