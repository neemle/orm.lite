using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

[Table("gs_demo_projects")]
public sealed class GraphSaveProject
{
    [Key] public Guid Id { get; set; }
    public Guid? TeamId { get; set; }
    public string Title { get; set; } = "";
    [ForeignKey("fk_gs_demo_project_team", ReferentialAction.Restrict)]
    public GraphSaveTeam? Team { get; set; }
    [Reference(nameof(GraphSaveTask.ProjectId))]
    public List<GraphSaveTask> Tasks { get; set; } = [];
    [ManyToMany("gs_demo_project_tags", "project_id", "tag_id")]
    public List<GraphSaveTag> Tags { get; set; } = [];
}
