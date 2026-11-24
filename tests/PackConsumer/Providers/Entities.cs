using Neemle.Orm.Lite;

namespace PackConsumer
{
    [Table("probe_docs")]
    public sealed class ProbeDoc
    {
        [Key] public Guid Id { get; set; }

        public string Name { get; set; } = "";
    }
}

[Table("probe_global_docs")]
public sealed class ProbeGlobalDoc
{
    [Key] public Guid Id { get; set; }

    public string Name { get; set; } = "";
}
