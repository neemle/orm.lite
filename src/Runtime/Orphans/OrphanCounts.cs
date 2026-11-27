namespace Neemle.OrmLite;

internal sealed record OrphanCounts(int Nullified, int Deleted, int SoftDeleted, int Inserted, int Updated)
{
    internal bool IsEmpty => Nullified == 0 && Deleted == 0 && SoftDeleted == 0 && Inserted == 0 && Updated == 0;
    internal bool IsValid => Nullified >= 0 && Deleted >= 0 && SoftDeleted >= 0 && Inserted >= 0 && Updated >= 0;
}
