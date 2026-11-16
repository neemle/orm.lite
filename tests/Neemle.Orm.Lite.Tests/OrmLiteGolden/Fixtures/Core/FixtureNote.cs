using Neemle.Orm.Lite;

namespace GoldenFixtures;

[Table("fixture_notes")]
public class FixtureNote
{
    [Key] public Guid Id { get; set; }
    [Indexed] public string Title { get; set; } = "";
    public int Revision { get; set; }
}
