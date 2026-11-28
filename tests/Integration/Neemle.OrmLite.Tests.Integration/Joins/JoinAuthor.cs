namespace Neemle.OrmLite.Tests.Integration.Joins;

[Table("it_join_authors")]
public sealed class JoinAuthor
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
