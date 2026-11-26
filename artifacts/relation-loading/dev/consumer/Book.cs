using Neemle.OrmLite;

namespace PolicySmoke;

[Table("policy_books", Managed = false)]
[RelationLoadLimits(MaxDepth = 8, MaxPaths = 32, MaxNodes = 64, MaxRoots = 100,
    MaxRelatedRows = 1000, MaxStatements = 64)]
public sealed class Book
{
    [Key] public int Id { get; set; }
    public int AuthorId { get; set; }
    [RelationLoad(RelationLoadMode.Eager)] public Author? Author { get; set; }
    public int? EditorId { get; set; }
    [RelationLoad(RelationLoadMode.Lazy)] public Author? Editor { get; set; }
}
