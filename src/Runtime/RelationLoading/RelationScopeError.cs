namespace Neemle.OrmLite;

public enum RelationScopeError
{
    InvalidOptions, UnsupportedNavigation, UnboundEntity, DifferentScope, EntityChanged,
    InvalidResource, Expired, Detached, Closed, QueueFull
}
