using System.Data.Common;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class FailureCases
{
    internal static async Task RunAsync(Fixture f)
    {
        await ProfileOrm.InsertAsync(f.Db, new Profile { Id = Guid.NewGuid(), PrincipalId = 2, Label = "fault" });
        var person = (await PersonOrm.GetAsync(f.Db, 2))!;
        var old = new Profile(); person.Profile = old;
        await using var native = await f.Db.DataSource.OpenConnectionAsync();
        var faults = new CleanupFaults();
        var connection = new FaultConnection(native, faults);
        MappingFaults.Enabled = true;
        try { await PersonOrm.LoadProfileAsync(f.Db, connection, person); }
        catch (Exception error)
        {
            Results.Require(ReferenceEquals(error, MappingFaults.Primary)
                && error.StackTrace?.Contains("set_Label", StringComparison.Ordinal) == true,
                "actual generated setter primary object/type/stack survives both cleanup failures");
        }
        finally { MappingFaults.Enabled = false; }
        Results.Require(faults.ReaderAttempts == 1 && faults.CommandAttempts == 1,
            "reader and command cleanup independently attempted");
        Results.Require(ReferenceEquals(old, person.Profile) && faults.CallerDisposals == 0
            && native.State == System.Data.ConnectionState.Open, "failure keeps old nav and borrowed connection");
        await CleanupOnlyAsync(f, person, native);
        Results.Case("failures",
            "generated setter primary retained|both cleanup attempts|cleanup-only refuses publication");
    }

    private static async Task CleanupOnlyAsync(Fixture f, Person person, DbConnection native)
    {
        var old = person.Profile;
        var faults = new CleanupFaults();
        try { await PersonOrm.LoadProfileAsync(f.Db, new FaultConnection(native, faults), person); }
        catch (Exception error)
        {
            Results.Require(ReferenceEquals(error, faults.ReaderError) || ReferenceEquals(error, faults.CommandError),
                "cleanup-only error is not swallowed");
            Results.Require(ReferenceEquals(old, person.Profile) && faults.ReaderAttempts == 1
                && faults.CommandAttempts == 1, "cleanup-only failure does not publish staged value");
            return;
        }
        throw new InvalidOperationException("Cleanup-only failure was swallowed");
    }

    internal static async Task ProviderAsync(Fixture f)
    {
        var person = (await PersonOrm.GetAsync(f.Db, 1))!;
        var old = new Profile(); person.Profile = old;
        await f.ExecuteAsync("DROP TABLE oto_profiles");
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        try { await PersonOrm.LoadProfileAsync(f.Db, connection, person); }
        catch (DbException)
        {
            Results.Require(ReferenceEquals(old, person.Profile)
                && connection.State == System.Data.ConnectionState.Open, "native provider failure keeps caller state");
            Results.Case("provider-failure", "native error propagated|old navigation and connection retained");
            return;
        }
        throw new InvalidOperationException("Expected actual missing-table provider failure");
    }
}
