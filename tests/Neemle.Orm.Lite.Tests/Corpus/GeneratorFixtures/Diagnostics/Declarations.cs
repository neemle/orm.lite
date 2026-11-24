using Neemle.Orm.Lite;

namespace CorpusDiagnostics;

[Table("cd_unmapped")]
public class UnmappedType
{
    [Key] public Guid Id { get; set; }
    public TimeSpan Span { get; set; }
    public object Bag { get; set; } = new();
}

[Table("cd_soft_missing")]
[SoftDelete("Missing")]
public class SoftColumnMissing
{
    [Key] public Guid Id { get; set; }
    public Guid TenantId { get; set; }
}

[Table("cd_soft_wrong_type")]
[SoftDelete("Removed")]
public class SoftColumnWrongType
{
    [Key] public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Removed { get; set; }
}

[Table("cd_soft_not_mapped")]
[SoftDelete]
public class SoftColumnNotMapped
{
    [Key] public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [NotAColumn] public DateTime? DeletedAt { get; set; }
}

[Table("cd_live_not_soft")]
[Index("Name", Unique = true, LiveOnly = true)]
[Index("Name", LiveOnly = true, Name = "ix_both_reasons")]
public class LiveOnlyOnPlainEntity
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

[Table("cd_live_not_unique")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
[Index("Name", LiveOnly = true)]
public class LiveOnlyNotUnique
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_duplicate_index")]
[Index("Name", Name = "ix_same")]
[Index("Code", Name = "ix_same")]
public class DuplicateIndexName
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

[Table("cd_no_tenant")]
[SoftDelete]
public class SoftWithoutTenant
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_tenant_wrong_type")]
[SoftDelete]
public class SoftTenantWrongType
{
    [Key] public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_named_tenant_missing")]
[SoftDelete(Tenant = "Nobody")]
public class SoftNamedTenantMissing
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_no_key")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class SoftWithoutKey
{
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_two_keys")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class SoftWithTwoKeys
{
    [Key] public Guid Id { get; set; }
    [Key] public Guid OtherId { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_key_not_guid")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class SoftKeyNotGuid
{
    [Key] public int Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_nullable_key")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class SoftNullableKey
{
    [Key] public Guid? Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_unknown_names")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Unversioned = ["NoSuch"], Label = "NoLabel", Owners = ["NoOwner"], Pinned = ["NoPin"], StatusColumns = ["NoStatus"])]
public class SoftUnknownNames
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_label_not_text")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Label = nameof(Rank))]
public class SoftLabelNotText
{
    [Key] public Guid Id { get; set; }
    public int Rank { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("cd_status_pinned")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Pinned = [nameof(State)], StatusColumns = [nameof(State)])]
public class SoftStatusPinned
{
    [Key] public Guid Id { get; set; }
    public string State { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Migration("0001_irreversible_with_down", Reversibility.Irreversible)]
public sealed class IrreversibleWithDown : Migration
{
    public override void Up(MigrationBuilder b) => b.Raw("postgres", "SELECT 1");

    public override void Down(MigrationBuilder b) => b.Raw("postgres", "SELECT 2");
}

[Migration("0002_unnamed_reversibility", (Reversibility)7)]
public sealed class UnnamedReversibility : Migration
{
    public override void Up(MigrationBuilder b) => b.Raw("postgres", "SELECT 1");
}

[Migration("0003_default_reversibility", default(Reversibility))]
public sealed class DefaultReversibility : Migration
{
    public override void Up(MigrationBuilder b) => b.Raw("postgres", "SELECT 1");
}
