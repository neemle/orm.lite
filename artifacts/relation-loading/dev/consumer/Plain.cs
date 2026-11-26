using Neemle.OrmLite;

namespace PolicySmoke;

[Table("policy_plain", Managed = false)]
public sealed class Plain
{
    [Key] public int Id { get; set; }
}

