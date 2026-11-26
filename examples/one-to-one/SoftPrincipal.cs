using Neemle.OrmLite;

namespace OneToOneDemo;

[Table("oto_soft_principals"), SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public sealed class SoftPrincipal
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
    [InverseOneToOne(nameof(SoftDependent.Parent))]
    public SoftDependent? Child { get; set; }
}
