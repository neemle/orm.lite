using Neemle.OrmLite;
namespace ComposeProbe;
[Table("compose_people")]
[RelationLoadLimits(MaxDepth=4, MaxPaths=16, MaxNodes=32, MaxRoots=10, MaxRelatedRows=100, MaxStatements=20)]
public sealed class Person
{
    [Key] public int Id { get; set; }
    public int GroupId { get; set; }
    public string? Note { get; set; }
    [RelationLoad(RelationLoadMode.Eager)] public Group? Group { get; set; }
    [InverseOneToOne(nameof(global::ComposeProbe.Profile.Person))] public Profile? Profile { get; set; }
}
