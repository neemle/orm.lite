using System.Diagnostics;

namespace OrmLiteDemo;

internal sealed class GraphSaveCommandCounter : IDisposable
{
    private readonly ActivityListener _listener;
    private int _attempted;
    internal int Attempted => Volatile.Read(ref _attempted);

    internal GraphSaveCommandCounter()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Neemle.OrmLite",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == "orm.graph-save") Interlocked.Increment(ref _attempted);
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();
}
