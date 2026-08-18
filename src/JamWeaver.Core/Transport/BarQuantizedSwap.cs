namespace JamWeaver.Core.Transport;

public sealed class BarQuantizedSwap<T> where T : class
{
    private readonly object _sync = new();
    private T _current;
    private T? _pending;
    private ulong _eligibleBar;

    public BarQuantizedSwap(T current) => _current = current ?? throw new ArgumentNullException(nameof(current));
    public T Current { get { lock (_sync) return _current; } }
    public T? Pending { get { lock (_sync) return _pending; } }

    public void Queue(T value, TransportState state, TransportPosition position)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_sync)
        {
            if (state != TransportState.Running)
            {
                _current = value;
                _pending = null;
                return;
            }
            _pending = value;
            _eligibleBar = position.Bar + 1;
        }
    }

    public bool TryActivate(TransportPosition position)
    {
        lock (_sync)
        {
            if (_pending is null || !position.IsBarBoundary || position.Bar < _eligibleBar) return false;
            _current = _pending;
            _pending = null;
            return true;
        }
    }

    public void Cancel() { lock (_sync) _pending = null; }
}
