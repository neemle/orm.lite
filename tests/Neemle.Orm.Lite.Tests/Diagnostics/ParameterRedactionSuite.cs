using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Diagnostics;

public abstract class ParameterRedactionSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private TFixture Fixture { get; } = fixture;

    [SkippableFact(DisplayName = "AC9 [SEC] a JSON secret written to a jsonb column and to a text column is redacted " +
        "in the report")]
    public async Task A_json_secret_is_redacted_in_the_reported_parameters()
    {
        Fixture.RequireAvailable();
        var sink = new RecordingSink();
        var options = Fixture.NewOptions();
        options.Diagnostics.Sink = sink;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        options.Diagnostics.LogParameters = true;
        await using var db = await OrmLite.OpenAsync(options);
        var row = new Row<BehaveNote>()
            .With(n => n.Id, Guid.NewGuid())
            .With(n => n.TenantId, Tenant)
            .With(n => n.Title, "{\"token\":\"visible-" + Guid.NewGuid().ToString("N") + "\"}")
            .With(n => n.Stars, 1)
            .With(n => n.Price, (decimal?)null)
            .With(n => n.Doc, Jsonb.Parse("{\"password\":\"hunter2\",\"kind\":\"red\"}"));

        await new Query<BehaveNote>(db, BehaveNote.Map).InsertManyAsync([row]);

        var values = sink.Statements.Where(s => s.Kind == StatementKind.Write)
            .SelectMany(s => s.Parameters!).Select(p => p.Value).ToList();
        Assert.True(values.Count(v => v == ParameterRedactor.Redacted) >= 2);
        Assert.DoesNotContain(values, v => v is not null && v.Contains("hunter2", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v is not null && v.Contains("visible-", StringComparison.Ordinal));
    }
}
