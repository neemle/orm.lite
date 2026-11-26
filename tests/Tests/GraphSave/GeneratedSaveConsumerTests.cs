using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class GeneratedSaveConsumerTests
{
    // UC-006 promises an ordinary external consumer can use generated tokens and registration.
    // This fixture compiles those calls, rather than accepting generated text as the oracle.
    internal const string Source = """
        using System.Collections.Generic;
        using Neemle.OrmLite.Generated;
        [Table("gs112_parent")]
        public sealed class SaveParent
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
            [Reference(nameof(SaveChild.ParentId))]
            public List<SaveChild> Children { get; set; } = new();
            [ManyToMany("gs112_parent_tags", "parent_id", "tag_id")]
            public List<SaveTag> Tags { get; set; } = new();
        }
        [Table("gs112_child")]
        public sealed class SaveChild
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
            public SaveParent? Parent { get; set; }
            public string Name { get; set; } = "";
        }
        [Table("gs112_tag")]
        public sealed class SaveTag
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }
        public static class SaveConsumer
        {
            public static GraphSaveRequest<SaveParent> Request()
            {
                var root = new SaveParent { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };
                var child = new SaveChild { Id = Guid.Parse("22222222-2222-2222-2222-222222222222") };
                var tag = new SaveTag { Id = Guid.Parse("33333333-3333-3333-3333-333333333333") };
                root.Children.Add(child);
                root.Tags.Add(tag);
                var permissions = GraphSavePermissions.None
                    .Allow(SaveParentOrm.SaveRelations.Children,
                        new GraphSaveRule(GraphSaveActions.Insert, GraphSaveKeyBinding.BindInWriteBuffer))
                    .Allow(SaveParentOrm.SaveRelations.Tags,
                        new GraphSaveRule(GraphSaveActions.Insert | GraphSaveActions.AddLink));
                var intents = GraphSaveIntents.Empty.With(child, GraphSaveIntent.Insert)
                    .With(tag, GraphSaveIntent.Insert);
                return new(root, GraphSaveIntent.Insert, new(intents, permissions,
                    new(new(2, 3, 2), new(3, 1, 4))));
            }
        }
        """;

    [Fact(DisplayName = "[UC-006][AC-112-01/02/05/06] generated external save consumer compiles and resolves exact tokens")]
    public void Generated_consumer_compiles_loads_and_constructs_request()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(d => d.ToString())));
        var model = GeneratedModel.Of(output);
        model.RunModuleInitializer();
        var consumer = model.Class("SaveParentOrm").Assembly.GetType("Fixture.SaveConsumer", true)!;
        var request = consumer.GetMethod("Request")!.Invoke(null, null);
        Assert.NotNull(request);
        Assert.Equal("GraphSaveRequest`1", request.GetType().Name);
    }
}
