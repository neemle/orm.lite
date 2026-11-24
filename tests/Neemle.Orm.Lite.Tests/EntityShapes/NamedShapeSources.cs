namespace Neemle.Orm.Lite.Tests.EntityShapes;

internal static class NamedShapeSources
{
    internal const string NameId = "ORMLITE025";

    internal const string NestedPublic = """
        public class Outer
        {
            [Table("inners")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
                public string Title { get; set; } = "";
            }
        }
        """;

    internal const string NestedDeep = """
        public class Outer
        {
            public class Mid
            {
                [Table("inners")]
                public class Inner
                {
                    [Key] public Guid Id { get; set; }
                }
            }
        }
        """;

    internal const string NestedInternal = """
        public class Outer
        {
            [Table("inners")]
            internal class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string NestedInInternal = """
        internal class Outer
        {
            [Table("inners")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string NestedProtectedInternal = """
        public class Outer
        {
            [Table("inners")]
            protected internal class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string NestedSoft = """
        public class Outer
        {
            [Table("inners")]
            [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
                public DateTime? DeletedAt { get; set; }
            }
        }
        """;

    internal const string NestedLinked = """
        public class Outer
        {
            [Table("inners")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
                public System.Collections.Generic.List<Holder> Holders { get; set; } = new();
            }
        }

        [Table("holders")]
        public class Holder
        {
            [Key] public Guid Id { get; set; }
            public Guid InnerId { get; set; }
            [Reference("InnerId")] public Outer.Inner? Item { get; set; }
        }
        """;

    internal const string GlobalNested = """
        using System;
        using Neemle.Orm.Lite;

        public class Outer
        {
            [Table("inners")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string PrivateNested = """
        public class Outer
        {
            [Table("inners")]
            private class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string ProtectedNested = """
        public class Outer
        {
            [Table("inners")]
            protected class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string PrivateProtectedNested = """
        public class Outer
        {
            [Table("inners")]
            private protected class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string InPrivateOuter = """
        public class Outer
        {
            private class Mid
            {
                [Table("inners")]
                public class Inner
                {
                    [Key] public Guid Id { get; set; }
                }
            }
        }
        """;

    internal const string FileLocal = """
        [Table("hiddens")]
        file class Hidden
        {
            [Key] public Guid Id { get; set; }
        }
        """;

    internal const string GenericOne = """
        [Table("gens")]
        public class Gen<T>
        {
            [Key] public Guid Id { get; set; }
            public T Value { get; set; } = default!;
        }
        """;

    internal const string GenericTwo = """
        [Table("gens")]
        public class Gen<TKey, TValue>
        {
            [Key] public Guid Id { get; set; }
            public TKey Code { get; set; } = default!;
            public TValue Value { get; set; } = default!;
        }
        """;

    internal const string GenericConstrained = """
        [Table("gens")]
        public class Gen<T> where T : struct
        {
            [Key] public Guid Id { get; set; }
            public T Value { get; set; }
        }
        """;

    internal const string NestedInGeneric = """
        public class Outer<T>
        {
            [Table("inners")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string GenericView = """
        [View("gens")]
        public class Gen<T>
        {
            public Guid Id { get; set; }
            public T Value { get; set; } = default!;
        }
        """;

    internal const string GenericReferrer = """
        [Table("gens")]
        public class Gen<T>
        {
            [Key] public Guid Id { get; set; }
            public T Value { get; set; } = default!;
        }

        [Table("holders")]
        public class Holder
        {
            [Key] public Guid Id { get; set; }
            public Guid GenId { get; set; }
            [Reference("GenId")] public Gen<int>? Instance { get; set; }
            public System.Collections.Generic.List<Gen<string>> Gens { get; set; } = new();
            [ManyToMany("holder_gens", "holder_id", "gen_id")]
            public System.Collections.Generic.List<Gen<long>> Linked { get; set; } = new();
        }
        """;

    internal const string SameName = """
        namespace Alpha
        {
            [Table("alpha_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public string Body { get; set; } = "";
                public System.Collections.Generic.List<Beta.Item> Items { get; set; } = new();
            }
        }

        namespace Beta
        {
            [Table("beta_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public string Body { get; set; } = "";
            }

            [Table("items")]
            public class Item
            {
                [Key] public Guid Id { get; set; }
                public Guid NoteId { get; set; }
                public Guid MateId { get; set; }
                [Reference("NoteId")] public Alpha.Note? Parent { get; set; }
                [Reference("MateId")] public Note? Mate { get; set; }
            }
        }
        """;

    internal const string SameNameSoft = """
        namespace Alpha
        {
            [Table("alpha_notes")]
            [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public DateTime? DeletedAt { get; set; }
            }
        }

        namespace Beta
        {
            [Table("beta_notes")]
            [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public DateTime? DeletedAt { get; set; }
            }
        }
        """;

    internal const string SameNameNested = """
        [Table("notes")]
        public class Note
        {
            [Key] public Guid Id { get; set; }
        }

        public class Outer
        {
            [Table("outer_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    internal const string SameNameLinks = """
        namespace Alpha
        {
            [Table("alpha_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
            }
        }

        namespace Beta
        {
            [Table("beta_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
                public Guid HolderId { get; set; }
            }
        }

        [Table("holders")]
        public class Holder
        {
            [Key] public Guid Id { get; set; }
            public System.Collections.Generic.List<Beta.Note> Betas { get; set; } = new();
            [ManyToMany("holder_alphas", "holder_id", "note_id")]
            public System.Collections.Generic.List<Alpha.Note> Alphas { get; set; } = new();
        }
        """;

    internal const string GenericSharesName = """
        namespace A
        {
            [Table("a_notes")]
            public class Note<T>
            {
                [Key] public Guid Id { get; set; }
                public T Value { get; set; } = default!;
            }
        }

        namespace B
        {
            [Table("b_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
            }

            [Table("holders")]
            public class Holder
            {
                [Key] public Guid Id { get; set; }
                public Guid NoteId { get; set; }
                [Reference("NoteId")] public Note? Mate { get; set; }
            }
        }
        """;

    internal const string StaticSharesName = """
        namespace A
        {
            [Table("a_notes")]
            public static class Note
            {
            }
        }

        namespace B
        {
            [Table("b_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
            }

            [Table("holders")]
            public class Holder
            {
                [Key] public Guid Id { get; set; }
                public Guid NoteId { get; set; }
                [Reference("NoteId")] public Note? Mate { get; set; }
            }
        }
        """;

    internal const string GlobalStaticSharesName = """
        using System;
        using Neemle.Orm.Lite;

        [Table("a_notes")]
        public static class Note
        {
        }

        namespace B
        {
            [Table("b_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
            }

            [Table("holders")]
            public class Holder
            {
                [Key] public Guid Id { get; set; }
                public Guid NoteId { get; set; }
                [Reference("NoteId")] public Note? Mate { get; set; }
            }
        }
        """;

    internal const string GlobalGenericsShareName = """
        using System;
        using Neemle.Orm.Lite;

        [Table("a_notes")]
        public class Note<T>
        {
            [Key] public Guid Id { get; set; }
            public T Value { get; set; } = default!;
        }

        [Table("c_notes")]
        public class Note<T1, T2>
        {
            [Key] public Guid Id { get; set; }
            public T1 First { get; set; } = default!;
            public T2 Second { get; set; } = default!;
        }

        namespace B
        {
            [Table("b_notes")]
            public class Note
            {
                [Key] public Guid Id { get; set; }
            }

            [Table("holders")]
            public class Holder
            {
                [Key] public Guid Id { get; set; }
                public Guid NoteId { get; set; }
                [Reference("NoteId")] public Note? Mate { get; set; }
            }
        }
        """;
}
