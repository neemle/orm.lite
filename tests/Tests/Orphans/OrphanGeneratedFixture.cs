using System.Reflection;
using Neemle.OrmLite.Tests.GraphSave;

namespace Neemle.OrmLite.Tests.Orphans;

internal static class OrphanGeneratedFixture
{
    internal static readonly Lazy<Task<Assembly>> Compiled = new(() =>
        SaveSdkCompiler.CompileFixture(Source, "OrphanConsumerFixture"));

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using Neemle.OrmLite;
        using Neemle.OrmLite.Nodes;
        namespace OrphanConsumerFixture;
        [Table("orphan_parent")]
        public sealed class Parent
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "parent";
            [Reference(nameof(Child.ParentId))]
            public List<Child> Children { get; set; } = new();
        }
        [Table("orphan_child")]
        public sealed class Child
        {
            [Key] public Guid Id { get; set; }
            public Guid? ParentId { get; set; }
            public string Name { get; set; } = "child";
            [ForeignKey("fk_orphan_child", ReferentialAction.SetNull)]
            public Parent? Parent { get; set; }
        }
        public static class Consumer
        {
            public static object Relation() => OrphanCollections.Relation<Parent, Child, Guid, Guid>("Children");
            public static object NewChild() => new Child { Id = new Guid(113, 0, 0, new byte[8]),
                ParentId = new Guid(7, 0, 0, new byte[8]) };
        }
        """;
}
