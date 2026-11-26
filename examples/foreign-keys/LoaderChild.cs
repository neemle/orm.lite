using Neemle.OrmLite;

namespace ForeignKeysDemo;

[Table("demo_fk_loader_children")]
public sealed class LoaderChild
{
    [Key] public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public Parent? Parent { get; set; }
}
