namespace Neemle.Orm.Lite.Tests.EntityShapes;

internal static class InheritedShapeSources
{
    internal const string KeyAndSoft = """
        public abstract class Entity
        {
            [Key] public Guid Id { get; set; }
            public DateTime? DeletedAt { get; set; }
        }

        [Table("docs")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class Doc : Entity
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string GenericBase = """
        public abstract class Base<TId>
        {
            [Key] public TId Id { get; set; } = default!;
            public string Name { get; set; } = "";
        }

        [Table("pies")]
        public class Pie : Base<int>
        {
            public int Slices { get; set; }
        }
        """;

    internal const string Hiding = """
        public class Root
        {
            [Key] public Guid Id { get; set; }
            [Column("root_label")] public virtual string Label { get; set; } = "";
            [Column("root_note")] public string Note { get; set; } = "";
        }

        public class Mid : Root
        {
            [Column("mid_label")] public override string Label { get => base.Label; set => base.Label = value; }
        }

        [Table("leaves")]
        public class Leaf : Mid
        {
            [Column("leaf_note")] public new string Note { get; set; } = "";
            public int Rank { get; set; }
        }
        """;

    internal const string Required = """
        public abstract class Stamped
        {
            [Key] public Guid Id { get; set; }
            public required string Author { get; set; }
        }

        [Table("posts")]
        public class Post : Stamped
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string RequiredScratch = """
        public abstract class Stamped
        {
            [Key] public Guid Id { get; set; }
            [NotAColumn] public required string Scratch { get; set; }
        }

        [Table("posts")]
        public class Post : Stamped
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string PrivateSetter = """
        public abstract class Entity
        {
            [Key] public Guid Id { get; set; }
            public string Secret { get; private set; } = "";
        }

        [Table("docs")]
        public class Doc : Entity
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string InternalSetter = """
        public abstract class Entity
        {
            [Key] public Guid Id { get; set; }
            public string Code { get; internal set; } = "";
        }

        [Table("docs")]
        public class Doc : Entity
        {
        }
        """;

    internal const string EntityOfEntity = """
        [Table("animals")]
        public class Animal
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }

        [Table("dogs")]
        public class Dog : Animal
        {
            public string Breed { get; set; } = "";
        }
        """;

    internal const string LinkedKey = """
        public abstract class Keyed
        {
            public int Id { get; set; }
        }

        [Table("tags")]
        public class Tag : Keyed
        {
            public string Name { get; set; } = "";
        }

        [Table("posts")]
        public class Post
        {
            [Key] public Guid Id { get; set; }
            [ManyToMany("post_tags", "post_id", "tag_id")]
            public System.Collections.Generic.List<Tag> Tags { get; set; } = new();
        }
        """;

    internal const string ClassAttributeOnBase = """
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public abstract class Entity
        {
            [Key] public Guid Id { get; set; }
            public DateTime? DeletedAt { get; set; }
        }

        [Table("docs")]
        public class Doc : Entity
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string Attributed = """
        public abstract class Base
        {
            [Key] [Column("ident")] public Guid Id { get; set; }
            [NotAColumn] public string Scratch { get; set; } = "";
            [Index] public string Code { get; set; } = "";
        }

        [Table("docs")]
        public class Doc : Base
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string UnmappedType = """
        public abstract class Base
        {
            [Key] public Guid Id { get; set; }
            public byte[] RowVersion { get; set; } = [];
        }

        [Table("docs")]
        public class Doc : Base
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string SecondKey = """
        public abstract class Base
        {
            public Guid Id { get; set; }
        }

        [Table("docs")]
        public class Doc : Base
        {
            [Key] public string Code { get; set; } = "";
        }
        """;

    internal const string SoftWithoutColumn = """
        public abstract class Base
        {
            [Key] public Guid Id { get; set; }
        }

        [Table("docs")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class Doc : Base
        {
            public string Title { get; set; } = "";
        }
        """;

    internal const string Loaders = """
        public abstract class Owned
        {
            [Key] public Guid Id { get; set; }
            public Guid KidId { get; set; }
            [Reference("KidId")] public Kid? Fav { get; set; }
            public System.Collections.Generic.List<Kid> Kids { get; set; } = new();
        }

        [Table("parents")]
        public class Parent : Owned
        {
        }

        [Table("kids")]
        public class Kid
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
        }
        """;
}
