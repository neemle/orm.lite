using Neemle.OrmLite;

namespace OneToOneDemo;

[Table("oto_people")]
public sealed class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Note { get; set; }
    [CalculatedLower(nameof(Name))]
    public string? LowerName { get; set; }
    [InverseOneToOne(nameof(global::OneToOneDemo.Profile.Person))]
    public Profile? Profile { get; set; }
}
