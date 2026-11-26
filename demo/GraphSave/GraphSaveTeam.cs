using Neemle.OrmLite;

namespace OrmLiteDemo;

[Table("gs_demo_teams")]
public sealed class GraphSaveTeam
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
