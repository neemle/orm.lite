using Neemle.OrmLite;

namespace ForeignKeysAot;

[Table("aot_children")]
public sealed class NativeChild
{
    public Guid Id { get; set; }

    public Guid? NativeParentId { get; set; }

    public NativeParent? NativeParent { get; set; }
}
