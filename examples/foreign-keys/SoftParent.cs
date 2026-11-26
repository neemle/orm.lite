using Neemle.OrmLite;

namespace ForeignKeysDemo;

[Table("demo_fk_soft_parents"), SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public sealed class SoftParent
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}
