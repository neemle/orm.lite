namespace Neemle.OrmLite.Tests.SoftDelete.History;

// A registered soft entry seen as another scope shape: the same table, key and columns, but its Platform flag and its
// tenant column are the ones the test names (the tenant column equal to the key makes it a tenant-root entry).
internal sealed class HistoryShapeEntry(ISoftEntry inner, bool platform, string? tenantColumn) : ISoftEntry
{
    public Type ClrType => inner.ClrType;
    public string Table => inner.Table;
    public string EntityToken => inner.EntityToken;
    public string KeyColumn => inner.KeyColumn;
    public string SoftColumn => inner.SoftColumn;
    public string? TenantColumn => tenantColumn;
    public bool Platform => platform;
    public string? LabelColumn => inner.LabelColumn;
    public IReadOnlyList<string> Owners => inner.Owners;
    public IReadOnlyList<string> Unversioned => inner.Unversioned;
    public IReadOnlyList<string> Pinned => inner.Pinned;
    public IReadOnlyList<string> StatusColumns => inner.StatusColumns;
    public string? TypeColumnOfOwner(string ownerColumn) => inner.TypeColumnOfOwner(ownerColumn);
    public string? JsonNameOfColumn(string column) => inner.JsonNameOfColumn(column);
    public string? ColumnOfJsonName(string jsonName) => inner.ColumnOfJsonName(jsonName);
    public TResult Accept<TResult>(ISoftEntryVisitor<TResult> visitor) => inner.Accept(visitor);
}
