using Neemle.Orm.Lite;

namespace TenantOption;

[Table("to_notes")]
[SoftDelete]
public class Note
{
    [Key] public Guid Id { get; set; }
    public Guid ScopeId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

[Table("to_named")]
[SoftDelete(Tenant = nameof(TenantId))]
public class Named
{
    [Key] public Guid Id { get; set; }
    public Guid ScopeId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime? DeletedAt { get; set; }
}
