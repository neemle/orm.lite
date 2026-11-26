namespace Neemle.OrmLite;

public sealed class GraphRequestException : ArgumentException
{
    internal GraphRequestException(GraphRequestError error, string? path = null)
        : base($"Graph request refused: {error}.")
    { Error = error; Path = path; }

    public GraphRequestError Error { get; }
    public string? Path { get; }
}
