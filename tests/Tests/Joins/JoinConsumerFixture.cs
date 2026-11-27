namespace Neemle.OrmLite.Tests.Joins;

internal static class JoinConsumerFixture
{
    internal const string Source = """
        using System;
        using Neemle.OrmLite;
        using Neemle.OrmLite.Generated;

        [Table("join_probe")]
        public sealed class Probe
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string? Note { get; set; }
            public bool Enabled { get; set; }
            public int Rank { get; set; }
            public long Count { get; set; }
            public DateTime? Edited { get; set; }
            [CalculatedLower(nameof(Name))] public string Lower { get; set; } = "";
        }

        [Table("join_external", Managed = false)]
        public sealed class External
        {
            [Key] public Guid Id { get; set; }
            public string? Label { get; set; }
        }

        public static class Use
        {
            public static Query<int> ExistingUnconstrained(Query<int> query) => query;

            public static JoinSelection Capture(OrmContext database)
            {
                var left = ProbeOrm.Query(database).Where(x => x.Enabled).OrderBy(x => x.Rank).AsJoinSource("left");
                var right = ExternalOrm.Query(database).AsJoinSource("right");
                _ = left.Column(x => x.Id).Equal(right.Column(x => x.Id))
                    .And(left.Column(x => x.Rank).GreaterThanValue(1));
                var result = new JoinSelection();
                _ = result.AddRequired(left.Column(x => x.Name), "name");
                _ = result.Add(left.Column(x => x.Lower), "lowerName");
                _ = result.Add(right.Column(x => x.Label), "label");
                return result;
            }
        }
        """;
}
