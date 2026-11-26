using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

[Table("demo_fk_children")]
public sealed class ForeignKeyChild
{
    public const string ConstraintName = "fk_demo_child_parent";

    [Key] public Guid Id { get; set; }
    public Guid? ParentId { get; set; }

    // The scalar is nullable because a physical parent deletion sets it to NULL.
    [Reference(nameof(ParentId))]
    [ForeignKey(ConstraintName, ReferentialAction.SetNull)]
    public ForeignKeyParent? Parent { get; set; }
}
