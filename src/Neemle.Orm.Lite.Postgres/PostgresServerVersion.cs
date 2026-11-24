using System.Data.Common;
using System.Globalization;

namespace Neemle.Orm.Lite.Postgres;

// The oldest PostgreSQL major version the provider runs on. The JSON comparisons write pg_input_is_valid, which
// the server has from 16, and the reconcile writes CREATE OR REPLACE TRIGGER, which it has from 14; on an older
// server both fail at the first such statement, so the version is read once when the provider starts.
internal static class PostgresServerVersion
{
    internal const int MinimumMajor = 16;

    private const int MajorUnit = 10000;

    private const int SinceTenUnit = 100000;

    internal static async Task RequireAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PostgresDdlDataLiterals.ServerVersionQuery;
        var number = Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        Require(number);
    }

    // The number is the server's server_version_num: 160004 is 16.4, 90624 is 9.6.24.
    internal static void Require(int versionNumber)
    {
        if (versionNumber / MajorUnit >= MinimumMajor) return;
        throw new NotSupportedException(
            $"PostgreSQL {Display(versionNumber)} is older than the minimum {MinimumMajor} that provider 'postgres' "
            + "supports: JSON comparison needs pg_input_is_valid (16) and the index reconcile needs "
            + $"CREATE OR REPLACE TRIGGER (14). Use PostgreSQL {MinimumMajor} or newer.");
    }

    internal static string Display(int versionNumber) => versionNumber >= SinceTenUnit
        ? string.Create(CultureInfo.InvariantCulture, $"{versionNumber / MajorUnit}.{versionNumber % MajorUnit}")
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{versionNumber / MajorUnit}.{versionNumber / 100 % 100}.{versionNumber % 100}");
}
