using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

[Table("gs_demo_tasks")]
public sealed class GraphSaveTask
{
    [Key] public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    [ForeignKey("fk_gs_demo_task_project", ReferentialAction.Restrict)]
    public GraphSaveProject? Project { get; set; }
}
