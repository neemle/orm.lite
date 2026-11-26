namespace Neemle.OrmLite;

internal interface IPolicySession
{
    GraphLimits Limits { get; }
    IPolicyOperation Operation(GraphReadContext context);
    void Seed(GraphReadContext context);
    void Remember(object entity, IGraphEntityBinding binding);
}
