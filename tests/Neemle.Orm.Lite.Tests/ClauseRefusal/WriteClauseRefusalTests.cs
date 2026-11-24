using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class WriteClauseRefusalTests
{
    [Theory(
        DisplayName =
            "[SEC] a write takes no paging or ordering clause: Compile and Render refuse it, " +
            "naming the operation and every clause set")]
    [MemberData(nameof(ClauseMatrix.Refused), MemberType = typeof(ClauseMatrix))]
    public void A_write_with_a_paging_or_ordering_clause_is_refused(
        string provider, string entity, string operation, string clause, bool clauseFirst)
    {
        var shape = new ClauseShape(provider, operation, clause, clauseFirst);
        var target = ClauseTargets.Of(entity);

        var compiled = Assert.Throws<InvalidOperationException>(() => target.Compile(shape));
        var rendered = Assert.Throws<InvalidOperationException>(() => target.Render(shape));

        ClauseNames.Verify(compiled.Message, operation, clause);
        ClauseNames.Verify(rendered.Message, operation, clause);
    }

    [Theory(
        DisplayName =
            "a write without a paging or ordering clause renders exactly as it did before the refusal existed")]
    [MemberData(nameof(ClauseMatrix.Controls), MemberType = typeof(ClauseMatrix))]
    public void A_write_without_a_clause_renders_unchanged(string provider, string entity, string operation)
    {
        var shape = new ClauseShape(provider, operation, "None", false);
        var target = ClauseTargets.Of(entity);
        var expected = ClauseControlText.Expected(provider, entity, operation);

        Assert.Equal(expected, target.Compile(shape).Text);
        Assert.Equal(expected, target.Render(shape));
    }
}
