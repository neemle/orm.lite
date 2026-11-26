namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecurityRoot
{
    public string Id { get; set; } = "";
    public string Payload { get; set; } = "";
    public List<SecurityLeaf> Children { get; set; } = [];
    public List<SecurityLeaf> Peers { get; set; } = [];
}
