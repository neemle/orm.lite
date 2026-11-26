using Neemle.OrmLite;

namespace PolicySmoke;

[Table("policy_tags", Managed = false)]
public sealed class Tag
{
    [Key] public int Id { get; set; }
    private string? _name;
    public static bool FailMapping { get; set; }
    public string? Name
    {
        get => _name;
        set { if (FailMapping) throw new MapSentinelException(); _name = value; }
    }
}
