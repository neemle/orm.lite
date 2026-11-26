using Xunit;

namespace Neemle.OrmLite.Tests.SchemaDiff;

public sealed class SqlitePrimaryKeyNullabilityTests
{
    [Theory(DisplayName = "[SEC][UC-011] physical SQLite key nullability cannot hide a tightening difference")]
    [InlineData(SqliteKeyForm.NullableText)]
    [InlineData(SqliteKeyForm.ExplicitNotNullText)]
    [InlineData(SqliteKeyForm.IntegerAlias)]
    [InlineData(SqliteKeyForm.InlineIntegerDescending)]
    [InlineData(SqliteKeyForm.TableIntegerDescending)]
    [InlineData(SqliteKeyForm.StrictText)]
    [InlineData(SqliteKeyForm.WithoutRowidText)]
    public Task Actual_storage_and_metadata_agree_before_noop_or_refusal(SqliteKeyForm form) =>
        SqliteNullabilityContract.RunAsync(form);
}
