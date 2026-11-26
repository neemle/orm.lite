using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Nodes;
using static ForeignKeysAot.NativeCommands;

namespace ForeignKeysAot;

internal static class NativeProbe
{
    internal static async Task VerifyAsync(OrmContext db)
    {
        var contributions = ForeignKeysAotSchema.Instance.Contributions();
        await db.MigrateAsync(contributions);
        Require(Convert.ToInt64(await ScalarAsync(db, "PRAGMA foreign_keys")) == 1, "enforcement disabled");
        await using var connection = await db.DataSource.OpenConnectionAsync();
        var keys = await db.Provider.CreateCatalog(connection).ForeignKeysAsync("aot_children");
        Require(keys.Count == 1 && keys[0].Name == "fk_aot_child_parent", "catalog identity mismatch");
        Require(keys[0].OnDelete == ReferentialAction.SetNull, "delete action mismatch");
        Require(keys[0].Columns.SequenceEqual(["native_parent_id"]), "local column mismatch");
        Require(keys[0].ReferencedTable == "aot_parents", "referenced table mismatch");
        await ExerciseAsync(db);
        var history = await ScalarAsync(db, "SELECT count(*) FROM schema_migrations");
        await db.MigrateAsync(contributions);
        Require(Equals(history, await ScalarAsync(db, "SELECT count(*) FROM schema_migrations")), "history changed");
    }

    private static async Task ExerciseAsync(OrmContext db)
    {
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        await ScalarAsync(db, "INSERT INTO aot_parents(id) VALUES (@p0)", parent);
        await ScalarAsync(db, "INSERT INTO aot_children(id,native_parent_id) VALUES (@p0,@p1)", child, parent);
        Require(await NativeParentOrm.Query(db).CountAsync() == 1, "generated read failed");
        await RequireOrphanRefusalAsync(db);
        await ScalarAsync(db, "DELETE FROM aot_parents WHERE id=@p0", parent);
        Require(Convert.ToInt64(await ScalarAsync(db,
            "SELECT count(*) FROM aot_children WHERE native_parent_id IS NULL")) == 1, "SetNull failed");
    }

    private static async Task RequireOrphanRefusalAsync(OrmContext db)
    {
        try
        {
            await ScalarAsync(db, "INSERT INTO aot_children(id,native_parent_id) VALUES (@p0,@p1)",
                Guid.NewGuid(), Guid.NewGuid());
        }
        catch (DbException) { return; }
        throw new InvalidOperationException("orphan was admitted");
    }
}
