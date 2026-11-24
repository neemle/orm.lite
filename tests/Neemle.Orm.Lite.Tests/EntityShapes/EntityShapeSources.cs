namespace Neemle.Orm.Lite.Tests.EntityShapes;

internal static class EntityShapeSources
{
    internal const string InitNonKey = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public string Title { get; init; } = "";
        }
        """;

    internal const string InitKey = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; init; }
            public string Title { get; set; } = "";
        }
        """;

    internal const string RequiredWithSet = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required string Title { get; set; }
        }
        """;

    internal const string RequiredInit = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required string Title { get; init; }
        }
        """;

    internal const string RequiredNullable = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required string? Note { get; set; }
        }
        """;

    internal const string Mixed = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public string Code { get; init; } = "";
            public required string Title { get; set; }
            public string Note { get; set; } = "";
            public required int Score { get; init; }
        }
        """;

    internal const string ViewWithRequiredAndInit = """
        [View("v")]
        public class A
        {
            public Guid Id { get; set; }
            public required string Title { get; set; }
            public string Note { get; init; } = "";
        }
        """;

    internal const string SoftInit = """
        [Table("a")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public string Title { get; set; } = "";
            public DateTime? DeletedAt { get; init; }
        }
        """;

    internal const string EnumJsonbAndNullable = """
        public enum Level { Low, High }

        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required Level Level { get; init; }
            public Level? Prior { get; init; }
            public required Jsonb Body { get; set; }
            public Jsonb? Extra { get; init; }
            public int? Count { get; init; }
            public required decimal Price { get; set; }
            public DateTime When { get; init; }
        }
        """;

    internal const string SetsRequiredOnParameterless = """
        [Table("p")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required string Title { get; set; }
            public required int Score { get; set; }

            [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
            public A() { Title = ""; }
        }
        """;

    internal const string PlainTwin = """
        [Table("p")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public string Title { get; set; } = "";
            public int Score { get; set; }
        }
        """;

    internal const string SetsRequiredOnParameterisedOnly = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required string Title { get; set; }

            public A() { }

            [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
            public A(int seed) { Title = ""; }
        }
        """;

    internal const string SameNamedAttribute = """
        public static class Other
        {
            [System.AttributeUsage(System.AttributeTargets.Constructor)]
            public sealed class SetsRequiredMembersAttribute : System.Attribute { }
        }

        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public required string Title { get; set; }

            [Other.SetsRequiredMembers]
            public A() { Title = ""; }
        }
        """;

    internal const string Sibling = """
        [Table("sibs")]
        public class Sib
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    internal const string StaticEntity = """
        [Table("ses")]
        public static class SE
        {
        }
        """;

    internal const string StaticView = """
        [View("sevs")]
        public static class SE
        {
        }
        """;

    internal const string StaticSoft = """
        [Table("ses")]
        [SoftDelete]
        public static class SE
        {
        }
        """;

    internal const string StaticIndexed = """
        [Table("ses")]
        [Index("Name")]
        public static class SE
        {
        }
        """;

    internal const string StaticReferrer = """
        [Table("ses")]
        public static class SE
        {
        }

        [Table("sibs")]
        public class Sib
        {
            [Key] public Guid Id { get; set; }
            public Guid SeId { get; set; }
            [Reference("SeId")] public SE Se { get; set; }
            [Reference("SibId")] public System.Collections.Generic.List<SE> Ses { get; set; }
            [ManyToMany("sib_ses", "sib_id", "se_id")] public System.Collections.Generic.List<SE> Linked { get; set; }
        }
        """;
}
