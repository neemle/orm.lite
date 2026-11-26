using Neemle.OrmLite;

namespace Neemle.OrmLite.StreamingAot;

[Table("sdet_aot_stream_entries")]
[SoftDelete]
public sealed class StreamEntry
{
    [Key] public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int Rank { get; set; }
    public DateTime? DeletedAt { get; set; }
}
