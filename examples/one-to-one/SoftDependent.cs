using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

[Table("oto_soft_dependents"), SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public sealed class SoftDependent
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
    [OneToOne(nameof(ParentId), nameof(SoftPrincipal.Child))]
    [ForeignKey("fk_oto_soft", ReferentialAction.Restrict)]
    [OneToOneUnique("uq_oto_soft")]
    public SoftPrincipal? Parent { get; set; }
}
