namespace Neemle.OrmLite.Tests.Graph;

internal static class SdetGraphGeneratedSource
{
    internal const string Text = """
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Neemle.OrmLite.Generated;
        [Table("sdet_graph_parents", Managed = false)]
        public class Parent
        {
            public string Id { get; set; } = "";
            public string? Name { get; set; }
            public string? Secret { get; set; }
            [CalculatedLower(nameof(Name))] public string? LowerName { get; set; }
            public List<Child> Children { get; set; } = new();
        }
        [Table("sdet_graph_children", Managed = false)]
        public class Child
        {
            public string Id { get; set; } = "";
            public string? ParentId { get; set; }
            public Parent? Parent { get; set; }
            public string? EditorId { get; set; }
            public Parent? Editor { get; set; }
            public string? Title { get; set; }
            [CalculatedLower(nameof(Title))] public string? LowerTitle { get; set; }
        }
        [Table("sdet_graph_int_parents", Managed = false)]
        public class IntParent
        {
            public int Id { get; set; }
            public List<IntChild> Children { get; set; } = new();
        }
        [Table("sdet_graph_int_children", Managed = false)]
        public class IntChild
        {
            public int Id { get; set; }
            public int? IntParentId { get; set; }
            public IntParent? IntParent { get; set; }
        }
        public static class Operations
        {
            private static GraphLimits Limits() => new(new(4, 4, 8), new(10, 50, 20));
            public static async Task<object[]> Read(OrmContext db)
            {
                var request = new GraphRequest<Parent>(Limits(), ParentOrm.Paths.Children,
                    ParentOrm.Paths.Children.Then(ChildOrm.Paths.Editor));
                var roots = await ParentOrm.Query(db).OrderBy(p => p.Id).ToGraphListAsync(request);
                var ints = await IntParentOrm.Query(db).OrderBy(p => p.Id).ToGraphListAsync(
                    new GraphRequest<IntParent>(Limits(), IntParentOrm.Paths.Children));
                return new object[] { roots.Select(p => p.LowerName).ToArray(),
                    roots.Select(p => p.Children.Count).ToArray(), roots[0].Children[0].LowerTitle!,
                    ReferenceEquals(roots[1], roots[0].Children[0].Editor),
                    roots[1].Children[0].LowerTitle is null, ints.Select(p => p.Children.Count).ToArray() };
            }
            public static async Task<object[]> Masked(OrmContext db)
            {
                var request = new GraphRequest<Parent>(Limits(),
                    ParentOrm.Paths.Children.Then(ChildOrm.Paths.Editor));
                var roots = await ParentOrm.Query(db).Where(p => p.Id == "A").Redact(p => p.Secret)
                    .ToGraphListAsync(request);
                return new object[] { roots[0].LowerName!, roots[0].Secret is null,
                    roots[0].Children[0].Editor!.Secret is null,
                    roots[0].Children[0].Editor!.LowerName is null };
            }
            public static Task Unsafe(OrmContext db) => ParentOrm.Query(db).Redact(p => p.Name)
                .ToGraphListAsync(new GraphRequest<Parent>(Limits(), ParentOrm.Paths.Children));
        }
        """;
}
