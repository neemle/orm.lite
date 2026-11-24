namespace Neemle.Orm.Lite.Tests.EntityShapes;

internal static class MemberShapeSources
{
    internal const string Kid = """
        [Table("kids")]
        public class Kid
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
        }

        """;

    internal const string Tag = """
        [Table("tags")]
        public class Tag
        {
            [Key] public Guid Id { get; set; }
        }

        """;

    internal const string Owner = """
        [Table("owners")]
        public class Owner
        {
            [Key] public Guid Id { get; set; }
        }

        """;

    internal const string TagLink = "[ManyToMany(\"parent_tags\", \"parent_id\", \"tag_id\")] ";

    internal const string Kids = "public System.Collections.Generic.List<Kid> Kids";

    internal const string Tags = "public System.Collections.Generic.List<Tag> Tags";

    internal static string Table(string members) =>
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } " + members + " }\n";

    internal static string View(string members) =>
        "[View(\"v\")] public class V { public Guid Id { get; set; } " + members + " }\n";

    internal static string Parent(string members) =>
        Kid + Tag + "[Table(\"parents\")] public class Parent { [Key] public Guid Id { get; set; } " + members + " }\n";

    internal static string Pet(string members) =>
        Owner + "[Table(\"pets\")] public class Pet { [Key] public Guid Id { get; set; } "
        + "public Guid OwnerId { get; set; } "
        + members + " }\n";

    internal static string ParentViewing(string members) =>
        Kid + Tag + "[View(\"pv\")] public class PV { public Guid Id { get; set; } " + members + " }\n";

    internal static string Targets(string header, string constructor) =>
        $$"""
        [Table("sources")]
        public class Source
        {
            [Key] public Guid Id { get; set; }
            public Guid TargetId { get; set; }
            public Target? Target { get; set; }
            public System.Collections.Generic.List<Target> Targets { get; set; } = new();
            [ManyToMany("source_targets", "source_id", "target_id")]
            public System.Collections.Generic.List<Target> Mates { get; set; } = new();
        }

        [Table("targets")]
        {{header}}
        {
            {{constructor}}
            [Key] public Guid Id { get; set; }
            public Guid SourceId { get; set; }
            public Source? Source { get; set; }
            public System.Collections.Generic.List<Source> Sources { get; set; } = new();
            [ManyToMany("source_targets", "target_id", "source_id")]
            public System.Collections.Generic.List<Source> Mates { get; set; } = new();
        }
        """;

    internal static string ParentOf(string childKey, string reference = "") =>
        $$"""
        [Table("parents")]
        public class P
        {
            [Key] public Guid Id { get; set; }
            {{reference}} public System.Collections.Generic.List<C> Kids { get; set; } = new();
            public System.Collections.Generic.List<D> Others { get; set; } = new();
        }

        [Table("kids")]
        public class C
        {
            [Key] public Guid Id { get; set; }
            {{childKey}}
        }

        [Table("others")]
        public class D
        {
            [Key] public Guid Id { get; set; }
            public Guid PId { get; set; }
        }
        """;
}
