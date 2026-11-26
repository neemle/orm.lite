using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

[Table("demo_fk_restrict_children")]
public sealed class RestrictChild
{
    [Key] public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    [ForeignKey(FkNames.Restrict, ReferentialAction.Restrict)]
    public Parent? Parent { get; set; }
}
