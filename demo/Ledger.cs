// What this file shows
//   Money handled exactly. Two accounts exchange money in transfers; each transfer is one unit of work that locks both
//   accounts, moves the amount, writes two ledger entries (one per leg) and refuses to commit unless the legs cancel.
//   An overdraft is refused and leaves nothing behind. It is chapter 17 of the output.
//
// Library features exercised here
//   [Numeric(19, 4)] on a decimal property: the declared precision and scale. On PostgreSQL the column is numeric(19, 4).
//       On SQLite there is no decimal type, so the library stores the decimal as text under a collation of its own
//       that compares by value, with a check that the value fits; the capability is ExactDecimal, Native on
//       PostgreSQL and Emulated on SQLite. Either way a decimal is never turned into a double on the way.
//   UnitOfWork: one connection and one transaction. Anything done through the generated overloads that take work.Connection,
//       or through Query<T>.On(work), joins the transaction; CommitAsync() commits; leaving the block without it
//       rolls back.
//   Query<T>.ForUpdate(), capability RowLock (Native on PostgreSQL, Emulated on SQLite): read a row and keep other
//       writers away from it until the transaction ends. PostgreSQL writes FOR UPDATE. SQLite writes no clause:
//       its unit of work already takes the write lock when it begins (one writer at a time), which gives the same
//       protection, so ForUpdate is accepted and costs nothing there.
//   Set(...).UpdateAsync() and InsertManyAsync(Row<T>...): an UPDATE of chosen columns, and an insert of several rows
//       whose columns are chosen one by one with Row<T>().With(...).
//   AggregateSum on a decimal column: the sum is computed by the database and returned as an exact decimal on both providers.
//
// What to notice in the output
//   - savings 66.6667 and current 33.6333: 0.10 and 33.3333 moved from savings to current. The total of the two accounts
//     is 100.3000 before and after (printed with F4, four decimals, because the scale of a decimal read back can differ
//     between the providers: PostgreSQL returns the declared scale, SQLite the scale it was written with. The values
//     are equal, only their text would differ).
//   - 4 entries: two transfers, two legs each. The refused overdraft wrote none.
//   - "overdraft refused: True": the third transfer asks for 1000 from an account that has about 33.63.

using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

// An account with a balance. Table "ledger_accounts" (generated baseline). Balance is an exact decimal with up to 19
// digits, 4 after the point.
[Table("ledger_accounts")]
public sealed class LedgerAccount
{
    [Key] public Guid Id { get; set; }

    public string Owner { get; set; } = "";

    [Numeric(19, 4)] public decimal Balance { get; set; }
}

// One leg of a transfer: a signed amount on one account. The two legs of a transfer share a TransferId and must add up
// to zero. Table "ledger_entries".
[Table("ledger_entries")]
public sealed class LedgerEntry
{
    [Key] public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Guid TransferId { get; set; }

    [Numeric(19, 4)] public decimal Amount { get; set; }

    // A UTC instant: instants are stored as DateTime in UTC, the kind the library stores as it is.
    public DateTime PostedAt { get; set; }
}

internal static class LedgerChapter
{
    public static async Task<bool> RunAsync(OrmContext db)
    {
        // Two accounts with fresh GUIDs in the owner name, so the rows of each run are distinguishable. 100.10 + 0.20.
        var savings = new LedgerAccount { Id = Guid.NewGuid(), Owner = "savings " + Guid.NewGuid().ToString("N"), Balance = 100.10m };
        var current = new LedgerAccount { Id = Guid.NewGuid(), Owner = "current " + Guid.NewGuid().ToString("N"), Balance = 0.20m };
        await LedgerAccountOrm.InsertAsync(db, savings);
        await LedgerAccountOrm.InsertAsync(db, current);
        var before = await TotalAsync(db, savings.Id, current.Id);

        // Two transfers that succeed: 0.10 and then 33.3333 from savings to current.
        await TransferAsync(db, savings.Id, current.Id, 0.10m);
        await TransferAsync(db, savings.Id, current.Id, 33.3333m);
        // A transfer that must fail: 1000 from current, which holds far less. TransferAsync throws
        // InvalidOperationException("insufficient funds") before changing anything; the unit of work is left without a
        // commit, so it rolls back, and the caller sees the refusal as an exception.
        var refused = false;
        try
        {
            await TransferAsync(db, current.Id, savings.Id, 1000m);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        var savingsAfter = (await LedgerAccountOrm.GetAsync(db, savings.Id))!.Balance;
        var currentAfter = (await LedgerAccountOrm.GetAsync(db, current.Id))!.Balance;
        var after = await TotalAsync(db, savings.Id, current.Id);
        var entries = await LedgerEntryOrm.Query(db).Where(e => e.AccountId == savings.Id || e.AccountId == current.Id).CountAsync();
        Console.WriteLine($"17. ledger: savings {savingsAfter}, current {currentAfter}, total {before:F4} -> {after:F4}, {entries} entries, overdraft refused: {refused}");
        // Expected: the balances after two transfers, the total unchanged (100.10 + 0.20 = 100.30), two legs per
        // successful transfer, and the overdraft refused. Decimals are compared with ==, never by their text.
        return refused && savingsAfter == 66.6667m && currentAfter == 33.6333m && before == after && after == 100.30m && entries == 4;
    }

    // The sum of the two balances, computed by the database as an exact decimal. AggregatesAsync returns the row as
    // objects, hence the conversion.
    private static async Task<decimal> TotalAsync(OrmContext db, Guid first, Guid second)
    {
        var row = await LedgerAccountOrm.Query(db).Where(a => a.Id == first || a.Id == second).AggregateSum(a => a.Balance).AggregatesAsync();
        return Convert.ToDecimal(row[0]);
    }

    // One transfer, all or nothing.
    private static async Task TransferAsync(OrmContext db, Guid from, Guid to, decimal amount)
    {
        await using var work = await UnitOfWork.BeginAsync(db);
        // Read both accounts through the unit of work and lock them, so nobody else can change the balances between this
        // read and the writes below.
        var source = await LedgerAccountOrm.Query(db).On(work).Where(a => a.Id == from).ForUpdate().FirstOrDefaultAsync();
        var target = await LedgerAccountOrm.Query(db).On(work).Where(a => a.Id == to).ForUpdate().FirstOrDefaultAsync();
        if (source is null || target is null) throw new InvalidOperationException("unknown account");
        // Business rules: a positive amount, and no overdraft. Throwing leaves the block without CommitAsync.
        if (amount <= 0m || source.Balance < amount) throw new InvalidOperationException("insufficient funds");
        // The new balances are computed here, in .NET decimal arithmetic (the library has no decimal arithmetic inside
        // a statement), and written back as exact values. This is safe only because the rows are locked above.
        await LedgerAccountOrm.Query(db).On(work).Where(a => a.Id == from).Set(a => a.Balance, source.Balance - amount).UpdateAsync();
        await LedgerAccountOrm.Query(db).On(work).Where(a => a.Id == to).Set(a => a.Balance, target.Balance + amount).UpdateAsync();
        var transfer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        // Two ledger entries in one InsertManyAsync call: minus the amount on the source, plus the amount on the target, sharing one
        // TransferId.
        await LedgerEntryOrm.Query(db).On(work).InsertManyAsync(
        [
            new Row<LedgerEntry>().With(e => e.Id, Guid.NewGuid()).With(e => e.AccountId, from).With(e => e.TransferId, transfer).With(e => e.Amount, -amount).With(e => e.PostedAt, now),
            new Row<LedgerEntry>().With(e => e.Id, Guid.NewGuid()).With(e => e.AccountId, to).With(e => e.TransferId, transfer).With(e => e.Amount, amount).With(e => e.PostedAt, now),
        ]);
        // An invariant checked INSIDE the transaction, against what was really written: the legs of this transfer must
        // add up to zero. If they did not, the exception would roll back the balance changes and both entries.
        var sum = await LedgerEntryOrm.Query(db).On(work).Where(e => e.TransferId == transfer).AggregateSum(e => e.Amount).AggregatesAsync();
        if (Convert.ToDecimal(sum[0]) != 0m) throw new InvalidOperationException("the legs of a transfer must cancel");
        await work.CommitAsync();
    }
}
