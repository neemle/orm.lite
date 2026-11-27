namespace Neemle.OrmLite;

internal sealed record JoinSelectionEntry(JoinColumnReference Column, object Slot, string Alias, bool Required);
