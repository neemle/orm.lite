using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

[Table("oto_setnull_dependents")]
public sealed class SetNullDependent
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    [OneToOne(nameof(ParentId), nameof(ActionParent.Nullable))]
    [ForeignKey("fk_oto_setnull", ReferentialAction.SetNull)]
    [OneToOneUnique("uq_oto_setnull")]
    public ActionParent? Parent { get; set; }
}
