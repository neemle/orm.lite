using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

[Table("oto_cascade_dependents")]
public sealed class CascadeDependent
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    [OneToOne(nameof(ParentId), nameof(ActionParent.Cascade))]
    [ForeignKey("fk_oto_cascade", ReferentialAction.Cascade)]
    [OneToOneUnique("uq_oto_cascade")]
    public ActionParent? Parent { get; set; }
}
