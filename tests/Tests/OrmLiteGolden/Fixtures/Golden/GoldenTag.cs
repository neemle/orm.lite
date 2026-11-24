using Neemle.OrmLite;

namespace GoldenFixtures;

[Table("golden_tags")]
public class GoldenTag
{
    [Key] public Guid Id { get; set; }
    public string Label { get; set; } = "";
}
