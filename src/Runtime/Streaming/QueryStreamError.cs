namespace Neemle.OrmLite;

public enum QueryStreamError
{
    InvalidRequest, UnsupportedShape, FrozenParameterUnsupported, ParameterCapacity, ResourceUnavailable,
    ResourceMismatch, ConcurrentStream, ConcurrentAdvance, InvalidState, ProviderFailure, MappingFailure,
    CleanupFailure, AlreadyFaulted,
}
