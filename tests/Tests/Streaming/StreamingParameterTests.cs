using System.Collections;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class StreamingParameterTests
{
    private sealed class LazyValues : IEnumerable<int>
    {
        internal int Enumerations;
        public IEnumerator<int> GetEnumerator()
        {
            Enumerations++; throw new InvalidOperationException("no enumeration");
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact(DisplayName = "[SEC][UC-010] typed arrays freeze and copy freshly per enumeration")]
    public async Task Caller_and_driver_mutation_cannot_change_a_later_binding()
    {
        var values = new[] { 1, 2, 3 };
        var seen = new List<int[]>();
        var probe = new StreamProbe { Executing = command =>
        {
            var bound = Assert.IsType<int[]>(command.Parameters[0].Value);
            Assert.Equal(1, bound[0]);
            seen.Add(bound); bound[0] = 777;
        } };
        var source = new StreamProbeSource(probe);
        var statement = new Statement("captured read", [new StatementParameter(0, "values", values)],
            StatementKind.Read);
        var snapshot = new StreamReadSnapshot<int>(statement, r => r.GetInt32(0),
            new StreamBinding(source, Neemle.OrmLite.Postgres.PostgresProvider.Instance), null);
        var stream = new QueryStream<int>(snapshot, default);
        values[0] = 999;
        for (var i = 0; i < 2; i++)
        {
            await using var e = stream.GetAsyncEnumerator();
            Assert.True(await e.MoveNextAsync());
        }
        Assert.NotSame(values, seen[0]);
        Assert.NotSame(seen[0], seen[1]);
    }

    [Fact(DisplayName = "[SEC][UC-010] unsafe lazy and mutable array shapes refuse without invoking user enumeration")]
    public void Unknown_parameter_shapes_are_not_coerced_or_enumerated()
    {
        var lazy = new LazyValues();
        var error = Assert.Throws<QueryStreamException>(() => StreamParameterValues.Copy((object)lazy));
        Assert.Equal(QueryStreamError.FrozenParameterUnsupported, error.Error);
        Assert.Equal(0, lazy.Enumerations);
        Assert.Throws<QueryStreamException>(() => StreamParameterValues.Copy((object)new object[0]));
        Assert.Throws<QueryStreamException>(() => StreamParameterValues.Copy((object)new int[2, 2]));
    }
}
