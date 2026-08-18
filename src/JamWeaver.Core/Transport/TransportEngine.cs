namespace JamWeaver.Core.Transport;

public enum ClockSource { External, Internal }
public enum TransportState { Stopped, Running, ClockLost }

public readonly record struct TransportPosition(ulong Pulse)
{
    public ulong Bar => Pulse / 96;
    public int PulseInBar => (int)(Pulse % 96);
    public int Beat => (int)((Pulse / 24) % 4);
    public int PulseInBeat => (int)(Pulse % 24);
    public bool IsBarBoundary => PulseInBar == 0;
    public bool IsBeatBoundary => PulseInBeat == 0;
}

public sealed class TransportEngine
{
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private ClockSource _source;
    private TransportState _state;
    private ulong _nextPulse;
    private long? _lastExternalTimestamp;

    public TransportEngine(ClockSource source = ClockSource.External, TimeProvider? timeProvider = null)
    {
        _source = source;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event Action<TransportPosition>? PulseAccepted;
    public event Action<TransportState>? StateChanged;
    public ClockSource Source { get { lock (_sync) return _source; } }
    public TransportState State { get { lock (_sync) return _state; } }
    public TransportPosition Position { get { lock (_sync) return new(_nextPulse); } }

    public void SelectSource(ClockSource source)
    {
        TransportState? changed = null;
        lock (_sync)
        {
            if (_source == source && _state == TransportState.Stopped) return;
            _source = source;
            _lastExternalTimestamp = null;
            if (_state != TransportState.Stopped) { _state = TransportState.Stopped; changed = _state; }
        }
        if (changed is { } state) StateChanged?.Invoke(state);
    }

    public bool Process(ClockSource source, RealtimeMessage message)
    {
        Action<TransportPosition>? pulseHandler = null;
        Action<TransportState>? stateHandler = null;
        TransportPosition position = default;
        TransportState changedState = default;
        lock (_sync)
        {
            if (source != _source) return false;
            switch (message)
            {
                case RealtimeMessage.Start:
                    _nextPulse = 0;
                    _state = TransportState.Running;
                    RefreshExternalTimestamp(source);
                    changedState = _state;
                    stateHandler = StateChanged;
                    break;
                case RealtimeMessage.Continue:
                    if (_state != TransportState.Running)
                    {
                        _state = TransportState.Running;
                        changedState = _state;
                        stateHandler = StateChanged;
                    }
                    RefreshExternalTimestamp(source);
                    break;
                case RealtimeMessage.Stop:
                    if (_state != TransportState.Stopped)
                    {
                        _state = TransportState.Stopped;
                        changedState = _state;
                        stateHandler = StateChanged;
                    }
                    _lastExternalTimestamp = null;
                    break;
                case RealtimeMessage.Clock:
                    if (_state != TransportState.Running) return false;
                    position = new TransportPosition(_nextPulse);
                    _nextPulse = checked(_nextPulse + 1);
                    RefreshExternalTimestamp(source);
                    pulseHandler = PulseAccepted;
                    break;
            }
        }
        stateHandler?.Invoke(changedState);
        pulseHandler?.Invoke(position);
        return true;
    }

    public bool CheckExternalClockLoss(TimeSpan timeout)
    {
        if (timeout < TimeSpan.FromMilliseconds(100) || timeout > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        Action<TransportState>? handler = null;
        lock (_sync)
        {
            if (_source != ClockSource.External || _state != TransportState.Running || _lastExternalTimestamp is null) return false;
            if (_timeProvider.GetElapsedTime(_lastExternalTimestamp.Value, _timeProvider.GetTimestamp()) < timeout) return false;
            _state = TransportState.ClockLost;
            handler = StateChanged;
        }
        handler?.Invoke(TransportState.ClockLost);
        return true;
    }

    private void RefreshExternalTimestamp(ClockSource source)
    {
        if (source == ClockSource.External) _lastExternalTimestamp = _timeProvider.GetTimestamp();
    }
}
