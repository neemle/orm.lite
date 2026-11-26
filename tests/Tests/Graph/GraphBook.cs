namespace Neemle.OrmLite.Tests.Graph;

internal sealed class GraphBook
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public int? EditorId { get; set; }
    public GraphAuthor? Editor { get; set; }
}
