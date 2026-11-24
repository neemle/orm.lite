namespace Neemle.Orm.Lite.Tests.CompositeKey;

internal static class CompositeKeySources
{
    internal const string Line = """
        [Table("lines")]
        public class Line
        {
            [Key] public Guid OrderId { get; set; }
            [Key] public Guid ProductId { get; set; }
            public int Qty { get; set; }
        }
        """;

    internal const string CodeAndId = """
        [Table("coded")]
        public class Coded
        {
            [Key] public string Code { get; set; } = "";
            public Guid Id { get; set; }
            public string Note { get; set; } = "";
        }
        """;

    internal const string ThreeKeys = """
        [Table("slots")]
        public class Slot
        {
            [Key] public Guid RegionId { get; set; }
            [Key] public Guid SiteId { get; set; }
            [Key] public Guid SlotId { get; set; }
            public int Capacity { get; set; }
        }
        """;

    internal const string TwoKeysAndId = """
        [Table("stamps")]
        public class Stamp
        {
            [Key] public string Region { get; set; } = "";
            public Guid Id { get; set; }
            [Key] public string Code { get; set; } = "";
            public string Note { get; set; } = "";
        }
        """;

    internal const string KeyIdAndOther = """
        [Table("twins")]
        public class Twin
        {
            [Key] public Guid Id { get; set; }
            [Key] public Guid OtherId { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    internal const string SoftTwoKeys = """
        [Table("soft_pairs")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class SoftPair
        {
            [Key] public Guid Id { get; set; }
            [Key] public Guid OtherId { get; set; }
            public DateTime? DeletedAt { get; set; }
        }
        """;

    internal const string EnumLine = """
        public enum Level { Low, High }

        [Table("tones")]
        public class Tone
        {
            [Key] public Guid OrderId { get; set; }
            [Key] public Guid ProductId { get; set; }
            public Level Level { get; set; }
        }
        """;

    internal const string KeyOnId = """
        [Table("docs")]
        public class Doc
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    internal const string KeyElsewhere = """
        [Table("registers")]
        public class Register
        {
            [Key] public string Code { get; set; } = "";
            public string Name { get; set; } = "";
        }
        """;

    internal const string IdByNameOnly = """
        [Table("things")]
        public class Thing
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    internal const string ShadowKey = """
        [Table("shadowed")]
        public class Shadowed
        {
            [Key] public Guid Id { get; set; }
            [Key]
            [NotAColumn]
            public Guid Shadow { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    internal const string Keyless = """
        [Table("pairs")]
        public class Pair
        {
            public Guid LeftId { get; set; }
            public string Tag { get; set; } = "";
        }
        """;

    internal const string ViewWithTwoKeys = """
        [View("pair_view")]
        public class PairView
        {
            [Key] public Guid LeftId { get; set; }
            [Key] public Guid RightId { get; set; }
            public string Tag { get; set; } = "";
        }
        """;

    internal const string Pair = """
        [Table("pairs")]
        public class Pair
        {
            [Key] public Guid LeftId { get; set; }
            [Key] public Guid RightId { get; set; }
            public int Weight { get; set; }
        }
        """;

    internal const string NoteWithRelation = Pair + "\n" + """
        [Table("notes")]
        public class Note
        {
            [Key] public Guid Id { get; set; }
            public Guid PairId { get; set; }
            public Pair? Pair { get; set; }
        }
        """;

    internal const string NoteWithNullableRelation = Pair + "\n" + """
        [Table("notes")]
        public class Note
        {
            [Key] public Guid Id { get; set; }
            public Guid? PairId { get; set; }
            public Pair? Pair { get; set; }
        }
        """;

    internal const string PairWithCollection = """
        [Table("pairs")]
        public class Pair
        {
            [Key] public Guid LeftId { get; set; }
            [Key] public Guid RightId { get; set; }
            public System.Collections.Generic.List<Part> Parts { get; set; } = new();
        }

        [Table("parts")]
        public class Part
        {
            [Key] public Guid Id { get; set; }
            public Guid PairId { get; set; }
            public string Label { get; set; } = "";
        }
        """;

    internal const string PairWithLinks = """
        [Table("pairs")]
        public class Pair
        {
            [Key] public Guid LeftId { get; set; }
            [Key] public Guid RightId { get; set; }
            [ManyToMany("pair_tags", "pair_id", "tag_id")]
            public System.Collections.Generic.List<Tag> Tags { get; set; } = new();
        }

        [Table("tags")]
        public class Tag
        {
            [Key] public Guid Id { get; set; }
            public string Label { get; set; } = "";
        }
        """;

    internal const string AuthorLinkedToPairs = Pair + "\n" + """
        [Table("authors")]
        public class Author
        {
            [Key] public Guid Id { get; set; }
            [ManyToMany("author_pairs", "author_id", "pair_id")]
            public System.Collections.Generic.List<Pair> Pairs { get; set; } = new();
        }
        """;

    internal const string AuthorWithPairCollection = """
        [Table("authors")]
        public class Author
        {
            [Key] public Guid Id { get; set; }
            public System.Collections.Generic.List<Pair> Pairs { get; set; } = new();
        }

        [Table("pairs")]
        public class Pair
        {
            [Key] public Guid LeftId { get; set; }
            [Key] public Guid RightId { get; set; }
            public Guid AuthorId { get; set; }
        }
        """;

    internal const string LineWithBoss = """
        [Table("lines")]
        public class Line
        {
            [Key] public Guid OrderId { get; set; }
            [Key] public Guid ProductId { get; set; }
            public Guid BossId { get; set; }
            public Owner? Boss { get; set; }
        }

        [Table("owners")]
        public class Owner
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    internal const string NodeWithParent = """
        [Table("nodes")]
        public class Node
        {
            [Key] public Guid GroupId { get; set; }
            [Key] public Guid NodeId { get; set; }
            public Guid ParentId { get; set; }
            public Node? Parent { get; set; }
        }
        """;

    internal const string OrdinaryNeighbours = Pair + "\n" + """
        [Table("authors")]
        public class Author
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
            [ManyToMany("author_tags", "author_id", "tag_id")]
            public System.Collections.Generic.List<Tag> Tags { get; set; } = new();
            public System.Collections.Generic.List<Book> Books { get; set; } = new();
        }

        [Table("tags")]
        public class Tag
        {
            [Key] public Guid Id { get; set; }
            public string Label { get; set; } = "";
        }

        [Table("books")]
        public class Book
        {
            [Key] public Guid Id { get; set; }
            public Guid AuthorId { get; set; }
            public string Title { get; set; } = "";
            public Author? Author { get; set; }
        }
        """;

    internal const string LineWithQueries = Line + "\n" + """
        public static class LineQueries
        {
            public static System.Threading.Tasks.Task Add(OrmContext db, Guid order, Guid product, int qty) =>
                Neemle.Orm.Lite.Generated.LineOrm.InsertAsync(
                    db, new Line { OrderId = order, ProductId = product, Qty = qty });

            public static System.Threading.Tasks.Task<int> DeleteOne(OrmContext db, Guid order, Guid product) =>
                Neemle.Orm.Lite.Generated.LineOrm.Query(db)
                    .Where(l => l.OrderId == order && l.ProductId == product)
                    .DeleteAsync();
        }
        """;
}
