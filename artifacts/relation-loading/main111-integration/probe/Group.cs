using Neemle.OrmLite;
namespace ComposeProbe;
[Table("compose_groups")]
public sealed class Group
{
    [Key] public int Id { get; set; }
    public string? Label { get; set; }
}
