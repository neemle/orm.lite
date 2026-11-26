using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Nodes;

namespace OrmLiteDemo;

internal static class ForeignKeyChapter
{
    public static async Task<bool> RunAsync(OrmContext db)
    {
        var catalog = await CatalogAsync(db);
        var enabled = await EnforcementAsync(db);
        var parent = new ForeignKeyParent { Id = Guid.NewGuid() };
        var child = new ForeignKeyChild { Id = Guid.NewGuid(), ParentId = parent.Id };
        await ForeignKeyParentOrm.InsertAsync(db, parent);
        await ForeignKeyChildOrm.InsertAsync(db, child);
        child = await ForeignKeyChildOrm.GetAsync(db, child.Id)
            ?? throw new InvalidOperationException("The foreign-key child was not stored.");
        var updates = await NullableUpdatesAsync(db, child, parent.Id);
        var orphanInsert = await RefuseOrphanInsertAsync(db);
        var orphanUpdate = await RefuseOrphanUpdateAsync(db, child);
        await ForeignKeyParentOrm.DeleteAsync(db, parent.Id);
        var survivor = await ForeignKeyChildOrm.GetAsync(db, child.Id);
        var setNull = survivor is not null && survivor.ParentId is null;
        var cleaned = await ForeignKeyChildOrm.DeleteAsync(db, child.Id) == 1;
        Console.WriteLine($"16a. generated foreign key: catalog {catalog}, enforcement {enabled}, "
            + $"nullable updates {updates}");
        Console.WriteLine($"     orphan insert refused {orphanInsert}, orphan update refused {orphanUpdate}");
        Console.WriteLine($"     physical parent delete kept the child with NULL {setNull}, rows cleaned {cleaned}");
        return catalog && enabled && updates && orphanInsert && orphanUpdate && setNull && cleaned;
    }

    private static async Task<bool> CatalogAsync(OrmContext db)
    {
        await using var connection = await db.DataSource.OpenConnectionAsync();
        var keys = await db.Provider.CreateCatalog(connection).ForeignKeysAsync("demo_fk_children");
        return keys.Count == 1 && keys[0].Name == ForeignKeyChild.ConstraintName
            && keys[0].Columns.SequenceEqual(["parent_id"])
            && keys[0].ReferencedTable == "demo_fk_parents"
            && keys[0].ReferencedColumns.SequenceEqual(["id"])
            && keys[0].OnDelete == ReferentialAction.SetNull;
    }

    private static async Task<bool> EnforcementAsync(OrmContext db)
    {
        if (db.Provider.Id != "sqlite") return true;
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> NullableUpdatesAsync(OrmContext db, ForeignKeyChild child, Guid parent)
    {
        if (child.ParentId != parent) return false;
        child.ParentId = null;
        await ForeignKeyChildOrm.UpdateAsync(db, child);
        var cleared = await ForeignKeyChildOrm.GetAsync(db, child.Id);
        if (cleared is null || cleared.ParentId is not null) return false;
        child.ParentId = parent;
        await ForeignKeyChildOrm.UpdateAsync(db, child);
        return (await ForeignKeyChildOrm.GetAsync(db, child.Id))?.ParentId == parent;
    }

    private static async Task<bool> RefuseOrphanInsertAsync(OrmContext db)
    {
        var orphan = new ForeignKeyChild { Id = Guid.NewGuid(), ParentId = Guid.NewGuid() };
        try
        {
            await ForeignKeyChildOrm.InsertAsync(db, orphan);
        }
        catch (DbException)
        {
            return true;
        }
        await ForeignKeyChildOrm.DeleteAsync(db, orphan.Id);
        return false;
    }

    private static async Task<bool> RefuseOrphanUpdateAsync(OrmContext db, ForeignKeyChild child)
    {
        child.ParentId = Guid.NewGuid();
        try
        {
            await ForeignKeyChildOrm.UpdateAsync(db, child);
        }
        catch (DbException)
        {
            return true;
        }
        return false;
    }
}
