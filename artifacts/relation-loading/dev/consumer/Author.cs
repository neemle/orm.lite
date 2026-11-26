using Neemle.OrmLite;

namespace PolicySmoke;

[Table("policy_authors", Managed = false)]
[RelationLoadLimits(MaxDepth = 8, MaxPaths = 32, MaxNodes = 64, MaxRoots = 100,
    MaxRelatedRows = 1000, MaxStatements = 64)]
public sealed class Author
{
    [Key] public int Id { get; set; }
    public string? Name { get; set; }
    [RelationLoad(RelationLoadMode.Eager)] public List<Book> Books { get; set; } = [];
    [ManyToMany("policy_author_tags", "author_id", "tag_id")]
    [RelationLoad(RelationLoadMode.Lazy)] public List<Tag> Tags { get; set; } = [];
}
