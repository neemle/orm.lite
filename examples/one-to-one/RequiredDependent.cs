using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

[Table("oto_required_dependents")]
public sealed class RequiredDependent
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    [OneToOne(nameof(ParentId), nameof(ActionParent.Required))]
    [ForeignKey("fk_oto_required", ReferentialAction.NoAction)]
    [OneToOneUnique("uq_oto_required")]
    public ActionParent? Parent { get; set; }
}
