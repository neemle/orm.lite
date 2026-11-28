namespace Neemle.OrmLite.Tests.Integration.Joins;

[Table("it_join_books")]
public sealed class JoinBook
{
    [Key] public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public Guid? EditorId { get; set; }
}
