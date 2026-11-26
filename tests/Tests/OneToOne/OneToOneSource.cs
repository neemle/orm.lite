namespace Neemle.OrmLite.Tests.OneToOne;

internal static class OneToOneSource
{
    internal const string Text = """
        using System.Threading;
        using System.Threading.Tasks;
        using Neemle.OrmLite.Nodes;
        using Neemle.OrmLite.Generated;
        [Table("sdet_oto_parent")] public class Parent
        {
            [Key] public int Id { get; set; }
            [InverseOneToOne(nameof(Child.Parent))] public Child? Child { get; set; }
        }
        [Table("sdet_oto_child")] public class Child
        {
            [Key] public Guid Id { get; set; }
            public int? ParentId { get; set; }
            [OneToOne(nameof(ParentId), nameof(Parent.Child))]
            [ForeignKey("fk_sdet_oto", ReferentialAction.Restrict)]
            [OneToOneUnique("uq_sdet_oto")]
            public Parent? Parent { get; set; }
            private string _label = "";
            public static int MappedLabels;
            public string Label { get => _label; set { MappedLabels++; _label = value; } }
        }
        public static class Operations
        {
            public static async Task<bool> Duplicate(OrmContext db)
            {
                var source = (await ParentOrm.GetAsync(db, 1))!;
                var old = new Child(); source.Child = old;
                Child.MappedLabels = 0;
                await using var connection = await db.DataSource.OpenConnectionAsync();
                try { await ParentOrm.LoadChildAsync(db, connection, source); }
                catch (OneToOneCardinalityException)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM sdet_oto_child";
                    return ReferenceEquals(old, source.Child) && Child.MappedLabels == 1
                        && Convert.ToInt64(await command.ExecuteScalarAsync()) == 3;
                }
                return false;
            }
            public static async Task<bool> GuardBeforeCancellation(OrmContext db)
            {
                var old = new Parent(); var source = new Child { Parent = old };
                using var canceled = new CancellationTokenSource(); canceled.Cancel();
                try { await ChildOrm.LoadParentAsync(db, (System.Data.Common.DbConnection)null!, source,
                    canceled.Token); }
                catch (OneToOneDeclarationException error)
                { return error.Error == OneToOneError.UnknownProvenance && ReferenceEquals(old, source.Parent); }
                return false;
            }
        }
        """;
}
