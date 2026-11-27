using System.Reflection;
using Neemle.OrmLite.Tests.GraphSave;

namespace Neemle.OrmLite.Tests.Orphans;

internal static class OrphanSafetyFixture
{
    internal static readonly Lazy<Task<Assembly>> Compiled = new(() =>
        SaveSdkCompiler.CompileFixture(Source, "OrphanSafetyConsumer"));

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using Neemle.OrmLite;
        using Neemle.OrmLite.Nodes;
        namespace OrphanSafetyConsumer;

        public static class Witness { public static int Scalar; public static int Navigation; }

        [Table("u1_partial_scalar")]
        public sealed partial class PartialScalar
        {
            [Key] public Guid Id { get; set; }
            public partial string Name { get; set; }
        }
        public sealed partial class PartialScalar
        {
            public partial string Name { get { Witness.Scalar++; return "partial"; } set { } }
        }
        [Table("u1_custom_scalar")]
        public sealed class CustomScalar
        {
            [Key] public Guid Id { get; set; }
            public string Name { get { Witness.Scalar++; return "custom"; } set { } }
        }
        [Table("u1_partial_nav")]
        public sealed partial class PartialNavigation
        {
            [Key] public Guid Id { get; set; }
            [Reference(nameof(PartialChild.ParentId))]
            public partial List<PartialChild> Children { get; set; }
        }
        public sealed partial class PartialNavigation
        {
            public partial List<PartialChild> Children
            { get { Witness.Navigation++; return new(); } set { } }
        }
        [Table("u1_partial_child")]
        public sealed class PartialChild
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
            [ForeignKey("fk_partial", ReferentialAction.Cascade)]
            public PartialNavigation? Parent { get; set; }
        }
        [Table("u1_custom_nav")]
        public sealed class CustomNavigation
        {
            [Key] public Guid Id { get; set; }
            [Reference(nameof(CustomChild.ParentId))]
            public List<CustomChild> Children
            { get { Witness.Navigation++; return new(); } set { } }
        }
        [Table("u1_custom_child")]
        public sealed class CustomChild
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
            [ForeignKey("fk_custom", ReferentialAction.Cascade)]
            public CustomNavigation? Parent { get; set; }
        }
        [Table("u1_collection_nav")]
        public sealed class CustomCollection
        {
            [Key] public Guid Id { get; set; }
            [Reference(nameof(CollectionChild.ParentId))]
            public ICollection<CollectionChild> Children { get; set; } = new List<CollectionChild>();
        }
        [Table("u1_collection_child")]
        public sealed class CollectionChild
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
            [ForeignKey("fk_collection", ReferentialAction.Cascade)]
            public CustomCollection? Parent { get; set; }
        }
        [Table("u1_record_parent")]
        public sealed record RecordParent([property: Key] Guid Id)
        {
            public RecordParent() : this(Guid.Empty) { }
            [Reference(nameof(RecordChild.ParentId))]
            public List<RecordChild> Children { get; set; } = new();
        }
        [Table("u1_record_child")]
        public sealed record RecordChild([property: Key] Guid Id, Guid ParentId, string Name)
        {
            public RecordChild() : this(Guid.Empty, Guid.Empty, "") { }
            [ForeignKey("fk_record", ReferentialAction.Cascade)]
            public RecordParent? Parent { get; set; }
        }
        """;
}
