namespace Neemle.OrmLite.Tests.Graph.CiIntegration;

internal static class ComposedGraphSource
{
    internal const string Intent = "[ForeignKey(\"fk_compose_parent\", ReferentialAction.SetNull)]";
    internal const string Text = $$"""
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Neemle.OrmLite.Nodes;
        using Neemle.OrmLite.Generated;
        [Table("compose_fk_parents")] public class Parent
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
            [CalculatedLower(nameof(Name))] public string Lower { get; set; } = "";
            {{Intent}} public List<Child> Children { get; set; } = new();
        }
        [Table("compose_fk_children")] public class Child
        {
            [Key] public Guid Id { get; set; }
            public Guid? ParentId { get; set; }
            {{Intent}} public Parent? Parent { get; set; }
        }
        public static class ComposedOps
        {
            public static async Task<bool> Run(OrmContext db)
            {
                var parent = new Parent { Id = Guid.NewGuid(), Name = "MiXeD", Lower = "NOT STORED" };
                var child = new Child { Id = Guid.NewGuid(), ParentId = parent.Id };
                await ParentOrm.InsertAsync(db, parent);
                await ChildOrm.InsertAsync(db, child);
                var limits = new GraphLimits(new GraphTraversalLimits(2, 1, 2), new GraphWorkLimits(2, 10, 6));
                var request = new GraphRequest<Parent>(limits,
                    ParentOrm.Paths.Children.Then(ChildOrm.Paths.Parent));
                var roots = await ParentOrm.Query(db).Where(p => p.Id == parent.Id).ToGraphListAsync(request);
                var root = roots.Single();
                var correct = root.Lower == "mixed" && root.Children.Single().Id == child.Id
                    && ReferenceEquals(root, root.Children.Single().Parent);
                await ParentOrm.DeleteAsync(db, parent.Id);
                var remaining = await ChildOrm.GetAsync(db, child.Id);
                return correct && remaining is not null && remaining.ParentId is null;
            }
        }
        """;
}
