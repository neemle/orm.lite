using Neemle.OrmLite;

namespace ForeignKeysDemo;

internal static class ConfigurationCases
{
    internal static async Task EnabledAsync(Fixture f, string provider)
    {
        var enabled = provider != "sqlite" || Convert.ToInt64(await f.ScalarAsync("PRAGMA foreign_keys")) == 1;
        Results.Require(enabled, "effective enabled connection configuration");
        Results.Case("enforcement", "enabled; observed native orphan/action enforcement");
    }
    internal static async Task ExplicitlyDisabledSqliteAsync(Fixture f, string provider)
    {
        if (provider != "sqlite") return;
        await using var disabled = await Neemle.OrmLite.OrmLite.OpenAsync(new OrmLiteOptions
        {
            Provider = "sqlite", ConnectionString = "Data Source=" + f.SqlitePath
                + ";Foreign Keys=False;Pooling=False",
        });
        await using var connection = await disabled.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Explicit false must be honored");
        command.CommandText = "INSERT INTO demo_fk_cascade_children(id,parent_id) VALUES (@p0,@p1)";
        var id = command.CreateParameter(); id.ParameterName = "p0"; id.Value = Fixture.Id(1000); command.Parameters.Add(id);
        var parent = command.CreateParameter(); parent.ParameterName = "p1"; parent.Value = Fixture.Id(999); command.Parameters.Add(parent);
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("disabled insertion failed");
        command.Parameters.Remove(parent);
        command.CommandText = "DELETE FROM demo_fk_cascade_children WHERE id=@p0";
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("provider-option sqlite ForeignKeys=False PRAGMA=0 orphan=admitted (unenforced)");
    }
}
