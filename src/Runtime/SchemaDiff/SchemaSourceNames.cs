namespace Neemle.OrmLite;

internal static class SchemaSourceNames
{
    internal static void Validate(SchemaDiffIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var prefix = SchemaDiffNames.Required(identity.VersionPrefix);
        if (prefix.Length < 4 || !prefix.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            || StringComparer.Ordinal.Compare(prefix, "0002") < 0
            || StringComparer.Ordinal.Compare(prefix, "9900") >= 0
            || prefix.Contains("__schema_diff_coverage", StringComparison.Ordinal))
            throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        Identifier(identity.CSharpTypeName);
        foreach (var part in SchemaDiffNames.Required(identity.CSharpNamespace).Split('.')) Identifier(part);
    }

    private static void Identifier(string value)
    {
        SchemaDiffNames.Required(value);
        if (!(char.IsAsciiLetter(value[0]) || value[0] == '_')
            || !value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
    }
}
