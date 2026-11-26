namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecurityLeaf
{
    public string Id { get; set; } = "";
    public string? ParentId { get; set; }
    public string Payload { get; set; } = "";
    public SecurityRoot? Parent { get; set; }
}
