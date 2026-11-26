#nullable enable
using System;

namespace Neemle.OrmLite;

public sealed class ForeignKeyDeclarationException : InvalidOperationException
{
    internal ForeignKeyDeclarationException(ForeignKeyDeclarationError error, string? entity, string? navigation)
        : base("Foreign key declaration " + error + ". Entity: " + Safe(entity) + "; navigation: " + Safe(navigation))
    {
        Error = error;
        Entity = Safe(entity);
        Navigation = Safe(navigation);
    }

    public ForeignKeyDeclarationError Error { get; }

    public string? Entity { get; }

    public string? Navigation { get; }

    private static string? Safe(string? value) => ForeignKeyValidation.ValidName(value) ? value : null;
}
