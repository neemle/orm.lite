namespace Neemle.OrmLite;

public sealed record OneToOneLookupTarget(
    EntityDescriptor Entity, string MatchColumn, string Navigation, OneToOneDirection Direction);
