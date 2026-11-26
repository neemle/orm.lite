namespace Neemle.OrmLite.Tests.Graph;

internal sealed class GraphAuthor
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<GraphBook> Books { get; set; } = [];
    public List<GraphTag> Tags { get; set; } = [];
}
