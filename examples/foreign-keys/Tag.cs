using Neemle.OrmLite;

namespace ForeignKeysDemo;

[Table("demo_fk_tags")]
public sealed class Tag
{
    [Key] public Guid Id { get; set; }
    public string Label { get; set; } = "";
    [CalculatedLower(nameof(Label))] public string LowerLabel { get; set; } = "";
}
