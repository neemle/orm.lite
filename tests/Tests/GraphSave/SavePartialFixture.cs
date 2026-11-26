namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SavePartialFixture
{
    internal const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Neemle.OrmLite;
        using Neemle.OrmLite.Generated;
        namespace PartialSaveFixture;

        [Table("gs112_partial_scalar")]
        public sealed partial class PartialScalar
        {
            [Key] public Guid Id { get; set; }
            public partial string Name { get; set; }
        }
        public sealed partial class PartialScalar
        {
            public partial string Name
            {
                get { Witness.ScalarReads++; return "payload"; }
                set { }
            }
        }
        [Table("gs112_partial_list")]
        public sealed partial class PartialList
        {
            [Key] public Guid Id { get; set; }
            [Reference(nameof(PartialChild.ParentId))]
            public partial List<PartialChild> Children { get; set; }
        }
        public sealed partial class PartialList
        {
            public partial List<PartialChild> Children
            {
                get { Witness.NavigationReads++; return Witness.Children; }
                set { Witness.Children = value; }
            }
        }
        [Table("gs112_partial_child")]
        public sealed class PartialChild
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
            public PartialList? Parent { get; set; }
        }
        [Table("gs112_partial_control")]
        public sealed class OrdinaryRoot
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "ordinary";
        }
        public static class Witness
        {
            public static int ScalarReads;
            public static int NavigationReads;
            public static List<PartialChild> Children = new();
            private static readonly Guid RootId = new("11111111-1111-1111-1111-111111111111");
            private static readonly Guid ChildId = new("22222222-2222-2222-2222-222222222222");

            public static async Task<GraphSaveResult> Save(OrmContext context, string shape)
            {
                ScalarReads = NavigationReads = 0;
                if (shape == "scalar")
                    return await GraphSave.SaveAsync(context, Request(new PartialScalar { Id = RootId }));
                if (shape == "ordinary")
                    return await GraphSave.SaveAsync(context, Request(new OrdinaryRoot { Id = RootId }));
                var root = new PartialList { Id = RootId };
                var child = new PartialChild { Id = ChildId, ParentId = RootId };
                Children = new() { child };
                var edge = SaveRelations.Edge<PartialList, PartialChild>(nameof(PartialList.Children));
                var plan = new GraphSavePlan(GraphSaveIntents.Empty.With(child, GraphSaveIntent.Insert),
                    GraphSavePermissions.None.Allow(edge, new(GraphSaveActions.Insert)),
                    new(new(1, 2, 1), new(2, 1, 2)));
                return await GraphSave.SaveAsync(context, new GraphSaveRequest<PartialList>(root,
                    GraphSaveIntent.Insert, plan));
            }

            private static GraphSaveRequest<T> Request<T>(T root) where T : class =>
                new(root, GraphSaveIntent.Insert, new(GraphSaveIntents.Empty, GraphSavePermissions.None,
                    new(new(1, 1, 1), new(1, 1, 1))));
        }
        """;
}
