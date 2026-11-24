using Neemle.Orm.Lite;

namespace CorpusEntities;

public enum Severity { Low, High }

[Table("ce_accounts")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Label = nameof(Name))]
[Index("Code", Unique = true, LiveOnly = true)]
[Index("Name")]
public class Account
{
    [Key] public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Table("ce_projects")]
[SoftDelete(Label = nameof(Title), Unversioned = [nameof(UpdatedAt)], Pinned = [nameof(Title)], StatusColumns = [nameof(State)])]
[Index("TenantId", "Title", Unique = true, LiveOnly = true)]
[Index("TenantId", "State", Name = "ix_projects_state")]
public class Project
{
    [Key] public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    [Index] public string Title { get; set; } = "";
    public Severity State { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    [Reference("ProjectId")]
    public List<WorkItem> Tasks { get; set; } = new();

    [ManyToMany("ce_project_labels", "project_id", "label_id")]
    public List<Label> Labels { get; set; } = new();

    [ManyToMany("ce_project_members", "project_id", "member_id")]
    public List<Member> Members { get; set; } = new();
}

[Table("ce_tasks")]
[SoftDelete("RemovedAt", Tenant = nameof(OwnerScope), Owners = [nameof(ProjectId), nameof(OwnerScope)])]
[Index("ProjectId", "Name", Unique = true)]
public class WorkItem
{
    [Key] public Guid Id { get; set; }
    public Guid OwnerScope { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? RemovedAt { get; set; }

    public Project? Project { get; set; }

    [Reference("ParentId")]
    public WorkItem? Parent { get; set; }
}

[Table("ce_labels")]
public class Label
{
    [Key] public Guid Id { get; set; }
    public string Text { get; set; } = "";
}

[Table("ce_members")]
[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class Member
{
    [Key] public Guid Id { get; set; }
    public string Handle { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Table("ce_project_members", Managed = false)]
public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Guid MemberId { get; set; }
}

[Table("ce_everything")]
public class Everything
{
    [Key] public Guid Id { get; set; }
    public Guid? OptionalGuid { get; set; }
    public string Text { get; set; } = "";
    public string? OptionalText { get; set; }
    public int Count { get; set; }
    public int? OptionalCount { get; set; }
    public long Big { get; set; }
    public bool Flag { get; set; }
    public double Ratio { get; set; }
    public decimal Amount { get; set; }
    public DateTime Stamp { get; set; }
    public DateTime? OptionalStamp { get; set; }
    public DateOnly Day { get; set; }
    public DateOnly? OptionalDay { get; set; }
    public Severity Level { get; set; }
    public Severity? OptionalLevel { get; set; }
    public Jsonb Document { get; set; } = Jsonb.Parse("{}");
    public Jsonb? OptionalDocument { get; set; }
    [Column("custom_name")] public string Renamed { get; set; } = "";
    [Index] public string Searchable { get; set; } = "";
    [Index(Unique = true)] public string Unique { get; set; } = "";
    [NotAColumn] public string Ignored { get; set; } = "";
    public string ReadOnly { get; } = "";
    public static string Static { get; set; } = "";
}

[Table("ce_keyless")]
public class Keyless
{
    public string Note { get; set; } = "";
    public int Counter { get; set; }
}

[Table("ce_internal")]
internal class InternalEntity
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
