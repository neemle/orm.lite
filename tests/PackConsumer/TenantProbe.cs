using Neemle.Orm.Lite;

namespace PackConsumer;

[Table("probe_owned")]
[SoftDelete]
public sealed class ProbeOwned
{
    [Key] public Guid Id { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid OwnerId { get; set; }
}
