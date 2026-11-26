namespace Neemle.OrmLite;

internal sealed class StreamMappingFailure(Exception cause) : Exception("Query stream mapper failed", cause)
{
    public override string ToString() => "Query stream mapper failed";
}
