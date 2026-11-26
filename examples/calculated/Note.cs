using Neemle.OrmLite;

namespace CalculatedDemo;

[Table("demo_calculated_notes")]
[Index(nameof(Label))]
public sealed class Note
{
    [Key] public Guid Id { get; set; }
    public string? Label { get; set; }
    [Column("lower\"label")]
    [CalculatedLower(nameof(Label))] public string? LowerLabel { get; set; }
    public List<Child> Children { get; set; } = [];
    [ManyToMany("demo_calculated_links", "note_id", "tag_id")]
    public List<Tag> Tags { get; set; } = [];
}
