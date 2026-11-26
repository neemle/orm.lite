namespace Neemle.OrmLite.Tests.Graph;

internal sealed class SdetRegistryRow
{
    public int Id { get; set; }
    public int? OtherId { get; set; }
    public string? Name { get; set; }
    public GraphAuthor? Other { get; set; }
    public List<GraphBook> Children { get; set; } = [];
    public List<GraphTag> Tags { get; set; } = [];
}
