namespace Neemle.OrmLite;

/// <summary>One column as a provider's catalog reports it, compared by <see cref="OrphanGuardColumns"/>.</summary>
/// <param name="Name">The column name.</param>
/// <param name="Type">The declared type, in the provider's spelling.</param>
/// <param name="Nullable">Whether the column accepts NULL.</param>
/// <param name="Key">Whether the column is part of the primary key.</param>
/// <param name="Computed">Whether the database computes the value (a generated column).</param>
public sealed record OrphanGuardColumn(string Name, string Type, bool Nullable, bool Key, bool Computed);
