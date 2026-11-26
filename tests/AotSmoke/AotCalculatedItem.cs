using Neemle.OrmLite;

namespace AotSmoke;

[Table("aot_calculated")]
internal sealed class AotCalculatedItem
{
    [Key] public Guid Id { get; set; }

    public string? Label { get; set; }

    [CalculatedLower(nameof(Label))]
    public string? LowerLabel { get; set; }
}
