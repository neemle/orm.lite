using System.Globalization;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;
using Diff = global::Neemle.OrmLite.SchemaDiff;

namespace Neemle.OrmLite.Tests.SchemaDiff;

internal static class SqliteNullabilityContract
{
    internal static async Task RunAllAsync()
    {
        foreach (var form in Enum.GetValues<SqliteKeyForm>()) await RunAsync(form);
    }

    internal static async Task RunAsync(SqliteKeyForm form)
    {
        var directory = Directory.CreateTempSubdirectory("orml117-nullability-sdet-");
        try
        {
            SqliteProviderFactory.Register();
            await using var context = await OrmLite.OpenAsync(new OrmLiteOptions
                { Provider = "sqlite", Path = Path.Combine(directory.FullName, "key.sqlite"), Pooling = false });
            await SetupAsync(context, form);
            var target = Target(form);
            var observation = await Diff.ObserveAsync(context, target);
            var column = observation.Tables.Single().Columns.Single();
            Equal(Nullable(form), column.Nullable.GetKnown(), form + " physical key nullability");
            var report = Diff.Compare(target, observation);
            Equal(Status(form), report.Status, form + " comparison status");
            CheckReport(form, report);
            await CheckStoredNullsAsync(context, form);
        }
        finally { directory.Delete(recursive: true); }
    }

    private static async Task SetupAsync(OrmContext context, SqliteKeyForm form)
    {
        await using var connection = await context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Definition(form);
        await command.ExecuteNonQueryAsync();
        command.CommandText = "INSERT INTO pk_case(id) VALUES(NULL)";
        var rejected = false;
        try { await command.ExecuteNonQueryAsync(); }
        catch (SqliteException error) when (error.SqliteErrorCode == 19) { rejected = true; }
        var requiresValue = form is SqliteKeyForm.ExplicitNotNullText or SqliteKeyForm.StrictText
            or SqliteKeyForm.WithoutRowidText;
        Equal(requiresValue, rejected, form + " actual NULL insert rejection");
    }

    private static async Task CheckStoredNullsAsync(OrmContext context, SqliteKeyForm form)
    {
        await using var connection = await context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pk_case WHERE id IS NULL";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Equal(Nullable(form) ? 1L : 0L, count, form + " actual stored NULL after review");
    }

    private static void CheckReport(SqliteKeyForm form, SchemaDiffReport report)
    {
        if (Nullable(form))
            Equal(true, report.Changes.Any(c => c.Kind == SchemaDiffChangeKind.NullabilityChange
                && c.Object.Table == "pk_case" && c.Object.Column == "id"), form + " tightening diagnostic");
        try { Diff.Emit(report, new("0051_nullability", "Nullability.Generated", "RefusedRecipe")); }
        catch (SchemaDiffException error) when (error.Error == SchemaDiffError.UnsupportedDifference) { return; }
        throw new InvalidOperationException(form + " emitted an executable artifact from " + report.Status);
    }

    private static SchemaDiffTarget Target(SqliteKeyForm form)
    {
        var type = form is SqliteKeyForm.IntegerAlias or SqliteKeyForm.InlineIntegerDescending
            or SqliteKeyForm.TableIntegerDescending ? ColumnType.Int32 : ColumnType.Text;
        var schema = OrmSchema.Of("nullability117", EntityDescriptor.Of("ObservedKey", "pk_case", true)
            .Property("Id", "id", type, false, true));
        return new(schema, "physical-key-v1", new("isolated-nullability117", "main", ["pk_case"]),
            new(2, 5, 40, 5, 150));
    }

    private static bool Nullable(SqliteKeyForm form) =>
        form is SqliteKeyForm.NullableText or SqliteKeyForm.InlineIntegerDescending;

    private static SchemaDiffStatus Status(SqliteKeyForm form) =>
        Nullable(form) || form is SqliteKeyForm.StrictText or SqliteKeyForm.WithoutRowidText
            ? SchemaDiffStatus.ManualRequired : SchemaDiffStatus.NoOp;

    private static string Definition(SqliteKeyForm form) => form switch
    {
        SqliteKeyForm.NullableText => "CREATE TABLE pk_case(id TEXT PRIMARY KEY)",
        SqliteKeyForm.ExplicitNotNullText => "CREATE TABLE pk_case(id TEXT PRIMARY KEY NOT NULL)",
        SqliteKeyForm.IntegerAlias => "CREATE TABLE pk_case(id INTEGER PRIMARY KEY)",
        SqliteKeyForm.InlineIntegerDescending => "CREATE TABLE pk_case(id INTEGER PRIMARY KEY DESC)",
        SqliteKeyForm.TableIntegerDescending => "CREATE TABLE pk_case(id INTEGER, PRIMARY KEY(id DESC))",
        SqliteKeyForm.StrictText => "CREATE TABLE pk_case(id TEXT PRIMARY KEY) STRICT",
        SqliteKeyForm.WithoutRowidText => "CREATE TABLE pk_case(id TEXT PRIMARY KEY) WITHOUT ROWID",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static void Equal<T>(T expected, T actual, string property)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(property + ": expected " + expected + ", observed " + actual);
    }
}
