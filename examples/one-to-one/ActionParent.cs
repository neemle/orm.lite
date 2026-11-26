using Neemle.OrmLite;

namespace OneToOneDemo;

[Table("oto_action_parents")]
public sealed class ActionParent
{
    public Guid Id { get; set; }
    [InverseOneToOne(nameof(CascadeDependent.Parent))]
    public CascadeDependent? Cascade { get; set; }
    [InverseOneToOne(nameof(SetNullDependent.Parent))]
    public SetNullDependent? Nullable { get; set; }
    [InverseOneToOne(nameof(RequiredDependent.Parent))]
    public RequiredDependent? Required { get; set; }
}
