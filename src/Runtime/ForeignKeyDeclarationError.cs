namespace Neemle.OrmLite;

public enum ForeignKeyDeclarationError
{
    InvalidName, UnknownRelation, UnmappedColumn, IncompatibleKey, UnsupportedKey, UnmanagedEndpoint,
    ExternalBaseline, DuplicateName, ConflictingDeclaration, InvalidDeleteAction, NonNullableSetNull,
    UnsupportedLink, DependencyCycle,
}
