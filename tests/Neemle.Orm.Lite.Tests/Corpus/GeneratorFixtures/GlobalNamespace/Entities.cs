using Neemle.Orm.Lite;

public enum Climate { Mild, Harsh }

[Table("gn_orchards")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Label = nameof(Name))]
[Index("Name", Unique = true, LiveOnly = true)]
public class Orchard
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Climate Climate { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<Tree> Trees { get; set; } = new();
}

[Table("gn_trees")]
[Index("OrchardId", "Species", Unique = true)]
public class Tree
{
    [Key] public Guid Id { get; set; }
    public Guid OrchardId { get; set; }
    public string Species { get; set; } = "";
    public int Age { get; set; }

    public Orchard? Orchard { get; set; }
}

[Migration("0100_first_orchard", Reversibility.Reversible)]
public sealed class FirstOrchard : Migration
{
    public const string Id = "00000000-0000-0000-0000-00000000f00d";

    public override void Up(MigrationBuilder b) => b.InsertData("gn_orchards", ["id", "name", "climate"], [Id, "home", "Mild"]);

    public override void Down(MigrationBuilder b) => b.DeleteData("gn_orchards", "id", Id);
}
