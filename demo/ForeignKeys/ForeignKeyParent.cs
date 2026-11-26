using Neemle.OrmLite;

namespace OrmLiteDemo;

// Managed tables participate in the fresh generated baseline; the tracker keeps its manual migrations.
[Table("demo_fk_parents")]
public sealed class ForeignKeyParent
{
    [Key] public Guid Id { get; set; }
}
