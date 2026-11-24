// What this file shows
//   Soft delete, restore and a change history that records WHO changed a row. It is chapters 5, 6 and 7 of the output.
//   A Draft is deleted, the row stays in the table marked as deleted ("in the trash"), queries stop seeing it, it is
//   restored, and the history lists the three changes with the user who made them.
//
// Library features exercised here
//   [SoftDelete]: DraftOrm.DeleteAsync no longer removes the row; it sets the DeletedAt column. Every Query<Draft>
//       then adds "not deleted" to its WHERE on its own, unless the query says IncludingDeleted().
//   Tenant: a soft-delete entity is scoped to a tenant. With no [SoftDelete(Tenant = ...)] the tenant column is
//       TenantId (the default), which is why Draft has one, and why RestoreAsync takes a tenant id.
//   [Index(..., Unique = true, LiveOnly = true)]: a unique index that counts only rows that are not in the trash, so a
//       deleted draft does not block a new live draft with the same (tenant, title). It needs the capability
//       PartialIndex, which is Native on both providers.
//   DbActor.EnterUser(userId): everything written inside the using block is attributed to that user.
//   Row history, capability RowHistoryTriggers (Native on PostgreSQL, Emulated on SQLite): the database records
//       each update, soft delete and restore of a [SoftDelete] row in a history table through triggers, together with
//       the actor. PostgreSQL has triggers natively. SQLite has them too, but they call functions the library
//       registers on its own connections to find the actor; that is the "emulation", and it gives the same results
//       on both providers, so the output of this chapter is the same on both.
//   RowHistoryRead.VersionsAsync: reads the history of one row back, under the tenant the row belongs to.
//
// What to notice in the output
//   - step 5: after the delete, 0 live drafts but 1 when the trash is included: the row was kept.
//   - step 6: after the restore, 1 live draft.
//   - step 7: three history lines (restore, soft_delete, update: newest first), all by the same user id. The INSERT
//     is not in the history, only later changes. The user id differs on every run because it is generated.

using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

// A draft document. Table "drafts" (created by the generated baseline: the entity is Managed).
[Table("drafts")]
[SoftDelete]
// Unique (tenant_id, title) among live rows only (LiveOnly = true).
[Index("tenant_id", "title", Unique = true, LiveOnly = true)]
public sealed class Draft
{
    [Key] public Guid Id { get; set; }

    // The tenant column named by the SoftDelete defaults (TenantId).
    public Guid TenantId { get; set; }

    public string Title { get; set; } = "";

    public int Stars { get; set; }

    // The soft-delete column (the default name is DeletedAt). Null means live; a timestamp means "in the trash".
    // The library sets and clears it; the program does not write it.
    public DateTime? DeletedAt { get; set; }
}

internal static class TrashChapter
{
    // Runs steps 5, 6 and 7 and returns whether every value was what was expected.
    public static async Task<bool> RunAsync(OrmContext db)
    {
        var tenant = Guid.NewGuid();
        var author = Guid.NewGuid();
        var draft = new Draft { Id = Guid.NewGuid(), TenantId = tenant, Title = "first draft", Stars = 1 };

        // Everything inside this block is done "as" the author, so the history records that user. The insert is not
        // recorded in the history; the update and the delete are.
        using (DbActor.EnterUser(author))
        {
            await DraftOrm.InsertAsync(db, draft);
            draft.Stars = 2;
            await DraftOrm.UpdateAsync(db, draft);
            // A soft delete: the row stays, DeletedAt is set.
            await DraftOrm.DeleteAsync(db, draft.Id);
        }

        // A normal query hides the deleted row; IncludingDeleted() shows it. Both are limited to this tenant so that
        // the counts do not depend on any other row in the table.
        var live = await DraftOrm.Query(db).Where(d => d.TenantId == tenant).CountAsync();
        var withTrash = await DraftOrm.Query(db).IncludingDeleted().Where(d => d.TenantId == tenant).CountAsync();
        Console.WriteLine($"5. soft delete: {live} live draft(s), {withTrash} including the trash");

        // Restore takes the ids and the tenant they belong to, and clears DeletedAt. It is a change like any other,
        // so it is attributed to a user too.
        using (DbActor.EnterUser(author))
        {
            await DraftOrm.Query(db).RestoreAsync([draft.Id], tenant);
        }

        var restored = await DraftOrm.Query(db).Where(d => d.TenantId == tenant).CountAsync();
        Console.WriteLine($"6. restore: {restored} live draft(s)");

        // The history of this one row, newest change first: what happened (Op), which kind of actor and which user did it.
        var history = await RowHistoryRead.VersionsAsync(db, DraftOrm.Table, draft.Id, tenant);
        Console.WriteLine("7. history, who changed what:");
        foreach (var version in history)
            Console.WriteLine($"   {version.Op} by {version.ActorKind} {version.ActorUserId}");
        // The demo passes this step only if: 0 live rows after the delete, 1 with the trash included, 1 live row after the
        // restore, exactly the three operations above were recorded, and every one names the author.
        return live == 0 && withTrash == 1 && restored == 1
            && history.Select(v => v.Op).Order(StringComparer.Ordinal).SequenceEqual(["restore", "soft_delete", "update"])
            && history.All(v => v.ActorUserId == author);
    }
}
