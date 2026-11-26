using Neemle.OrmLite;

namespace CalculatedDemo;

[Table("demo_calculated_children")]
public sealed class Child
{
    [Key] public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Note? Note { get; set; }
    public string Title { get; set; } = "";
    [CalculatedLower(nameof(Title))] public string LowerTitle { get; set; } = "";
}
