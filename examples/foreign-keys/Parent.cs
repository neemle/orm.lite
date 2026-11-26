using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace ForeignKeysDemo;

[Table("demo_fk_parents")]
public sealed class Parent
{
    [Key] public Guid Id { get; set; }
    public string Label { get; set; } = "";
    [CalculatedLower(nameof(Label))] public string LowerLabel { get; set; } = "";
    [Reference(nameof(SetNullChild.ParentId))]
    [ForeignKey(FkNames.SetNull, ReferentialAction.SetNull)]
    public List<SetNullChild> Children { get; set; } = [];
    [ManyToMany("demo_fk_links", "parent_id", "tag_id")]
    [LinkForeignKey(LinkForeignKeyEnd.Source, FkNames.LinkSource, ReferentialAction.Restrict)]
    [LinkForeignKey(LinkForeignKeyEnd.Target, FkNames.LinkTarget, ReferentialAction.Cascade)]
    public List<Tag> Tags { get; set; } = [];
}
