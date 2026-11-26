using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysAot;

[Table("aot_parents")]
public sealed class NativeParent
{
    public Guid Id { get; set; }

    [ForeignKey("fk_aot_child_parent", ReferentialAction.SetNull)]
    public List<NativeChild> Children { get; set; } = [];
}
