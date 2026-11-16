using Neemle.Orm.Lite;

namespace PackConsumer;

[Table("probe_docs")]
public sealed class ProbeDoc
{
    [Key] public Guid Id { get; set; }

    public string Name { get; set; } = "";
}

[Table("probe_notes")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public sealed class ProbeNote
{
    [Key] public Guid Id { get; set; }

    public DateTime? DeletedAt { get; set; }
}

[Migration("0001_probe", Reversibility.Reversible)]
public sealed class ProbeMigration : Migration
{
    public override void Up(MigrationBuilder b) => b.Sql("SELECT 'up'");

    public override void Down(MigrationBuilder b) => b.Sql("SELECT 'down'");
}

public static class Probe
{
    public static string Setting => OrmLiteSettings.ActorSetting;
}
