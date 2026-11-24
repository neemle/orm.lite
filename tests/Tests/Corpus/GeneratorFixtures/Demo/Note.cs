using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("notes")]
[SoftDelete]
[Index("tenant_id", "title", Unique = true, LiveOnly = true)]
public sealed class Note
{
    [Key] public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public int Stars { get; set; }

    public DateTime? DeletedAt { get; set; }
}

[Migration("0100_welcome_note", Reversibility.Reversible)]
public sealed class WelcomeNote : Migration
{
    public const string Id = "00000000-0000-0000-0000-00000000d3a0";

    public override void Up(MigrationBuilder b) => b.InsertData(
        "notes",
        ["id", "tenant_id", "title", "body", "stars"],
        [Id, "00000000-0000-0000-0000-000000000000", "welcome", "added by an explicit migration", 0]);

    public override void Down(MigrationBuilder b) => b.DeleteData("notes", "id", Id);
}
