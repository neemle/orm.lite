namespace Neemle.OrmLite;

internal sealed class StreamTokens(CancellationToken terminal, CancellationToken enumeration)
{
    private readonly object _sync = new();
    private CancellationTokenSource? _private;
    private CancellationTokenSource? _userLink;
    private CancellationTokenSource? _operation;
    private bool _requested;

    internal bool UserCanceled => terminal.IsCancellationRequested || enumeration.IsCancellationRequested;
    internal CancellationToken UserToken => terminal.IsCancellationRequested ? terminal : enumeration;

    internal CancellationToken Effective()
    {
        lock (_sync)
        {
            if (_private is not null) return _operation?.Token ?? _private.Token;
            _private = new CancellationTokenSource();
            var user = User();
            if (user.CanBeCanceled) _operation = CancellationTokenSource.CreateLinkedTokenSource(user, _private.Token);
            if (_requested) _private.Cancel();
            return _operation?.Token ?? _private.Token;
        }
    }

    private CancellationToken User()
    {
        if (!terminal.CanBeCanceled) return enumeration;
        if (!enumeration.CanBeCanceled || terminal == enumeration) return terminal;
        _userLink = CancellationTokenSource.CreateLinkedTokenSource(terminal, enumeration);
        return _userLink.Token;
    }

    internal void RequestDispose(StreamCleanup cleanup)
    {
        lock (_sync)
        {
            _requested = true;
            try { _private?.Cancel(); }
            catch (Exception error) { cleanup.Add(error); }
        }
    }

    internal void Cleanup(StreamCleanup cleanup)
    {
        lock (_sync)
        {
            Dispose(_operation, cleanup);
            Dispose(_userLink, cleanup);
            Dispose(_private, cleanup);
            _operation = _userLink = _private = null;
        }
    }

    private static void Dispose(CancellationTokenSource? source, StreamCleanup cleanup)
    {
        try { source?.Dispose(); }
        catch (Exception error) { cleanup.Add(error); }
    }
}
