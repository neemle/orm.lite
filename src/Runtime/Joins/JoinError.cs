namespace Neemle.OrmLite;

public enum JoinError
{
    InvalidRequest, UnknownSource, DuplicateAlias, OutOfScopeSource, UnsupportedSource, UnsupportedOperation,
    UnsupportedExpression, UnsupportedType, UnsupportedVisibility, ResourceMismatch, InvalidProjection,
    DuplicateOutputAlias, RequiredNullable, LimitExceeded, ParameterCapacity, MappingFailure, MissingValue,
    WrongSlot, ProviderFailure,
}
