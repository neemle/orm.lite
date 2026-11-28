// §803 provenance: item ORML-134, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using Neemle.OrmLite.Postgres;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class PostgresWriterIdentityAttackTests
{
    [Fact(DisplayName =
        "F4 803 FULL round 1: DNS casing cannot change database writer identity")]
    public void Dns_case_variants_have_the_same_writer_identity()
    {
        using var lower = NpgsqlDataSource.Create(
            "Host=db.example.invalid;Port=5432;" +
            "Database=CaseSensitiveDb;Username=reviewer");

        using var upper = NpgsqlDataSource.Create(
            "Host=DB.EXAMPLE.INVALID;Port=5432;" +
            "Database=CaseSensitiveDb;Username=reviewer");

        var lowerKey = PostgresProvider.Instance.WriterKeyOf(lower);
        var upperKey = PostgresProvider.Instance.WriterKeyOf(upper);

        Assert.NotNull(lowerKey);
        Assert.Equal(lowerKey, upperKey);
    }
}
