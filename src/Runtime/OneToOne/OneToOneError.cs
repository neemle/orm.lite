namespace Neemle.OrmLite;

public enum OneToOneError
{
    InvalidOwner, InvalidInverse, AmbiguousPair, UnsupportedKey, UnmappedForeignKey, IncompatibleKey,
    InvalidNullability, MissingConstraintIntent, ConflictingConstraint, UnsupportedSchema,
    UnsupportedVisibility, UnknownProvenance,
}
