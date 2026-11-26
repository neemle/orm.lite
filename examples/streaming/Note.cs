using Neemle.OrmLite;

namespace StreamingExample;

[Table("streaming_example_notes")]
public sealed class Note
{
    [Key] public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int Stars { get; set; }
    public int? Estimate { get; set; }
}
