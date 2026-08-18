using JamWeaver.Core.Generation;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Performance;

public sealed class PatternPlayer : IDisposable
{
    private readonly object _sync = new();
    private readonly SafeMidiOutput _output;
    private readonly TransportEngine _transport;
    private readonly List<ScheduledNote> _scheduledNotes = [];
    private Pattern? _current;
    private Pattern? _pending;
    private ulong _currentTriggerKey;
    private ulong _pendingTriggerKey;
    private ulong _eligibleBar;
    private MidiChannel _channel = new(1);
    private bool _enabled;
    private Exception? _error;
    private ulong? _lastPulse;
    private bool _disposed;

    public PatternPlayer(SafeMidiOutput output, TransportEngine transport)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _transport.PulseAccepted += OnPulse;
        _transport.StateChanged += OnStateChanged;
    }

    public Pattern? CurrentPattern { get { lock (_sync) return _current; } }
    public Pattern? PendingPattern { get { lock (_sync) return _pending; } }
    public bool IsEnabled { get { lock (_sync) return _enabled; } }
    public Exception? Error { get { lock (_sync) return _error; } }
    public MidiChannel Channel
    {
        get { lock (_sync) return _channel; }
        set
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                if (_channel == value) return;
                SilenceUnsafe();
                _channel = value;
            }
        }
    }

    public void Queue(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var triggerKey = PatternTriggerKey.Create(pattern);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_transport.State != TransportState.Running)
            {
                _current = pattern;
                _currentTriggerKey = triggerKey;
                _pending = null;
                return;
            }
            _pending = pattern;
            _pendingTriggerKey = triggerKey;
            _eligibleBar = _transport.Position.Bar + 1;
        }
    }

    public bool ReplaceMetadata(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        lock (_sync)
        {
            ThrowIfDisposed();
            var matched = false;
            if (_current?.Id == pattern.Id) { _current = pattern; matched = true; }
            if (_pending?.Id == pattern.Id) { _pending = pattern; matched = true; }
            return matched;
        }
    }

    public void Play()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_current is null && _pending is null)
                throw new InvalidOperationException("Generate a candidate pattern before enabling playback.");
            _error = null;
            _enabled = true;
        }
    }

    public void Mute()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _enabled = false;
            SilenceUnsafe();
        }
    }

    private void OnPulse(TransportPosition position)
    {
        lock (_sync)
        {
            if (_disposed) return;
            try
            {
                if (_lastPulse is { } previous && position.Pulse <= previous) SilenceUnsafe();
                _lastPulse = position.Pulse;
                ReleaseDueNotes(position.Pulse);
                ActivatePending(position);
                if (!_enabled || _current is null) return;
                var pulsesPerStep = (ulong)_current.Timing.PulsesPerStep;
                if (position.Pulse % pulsesPerStep != 0) return;
                var absoluteStep = position.Pulse / pulsesPerStep;
                var stepIndex = (int)(absoluteStep % (ulong)_current.Steps.Length);
                var loopIndex = absoluteStep / (ulong)_current.Steps.Length;
                if (!StepTriggerDecision.ShouldTrigger(_currentTriggerKey, _current.Steps[stepIndex], loopIndex, stepIndex)) return;
                TriggerStep(_current, stepIndex, position.Pulse);
            }
            catch (Exception ex)
            {
                _error = ex;
                _enabled = false;
                try { SilenceUnsafe(); }
                catch (Exception cleanupError) { _error = new AggregateException(ex, cleanupError); }
            }
        }
    }

    private void ActivatePending(TransportPosition position)
    {
        if (_pending is null || !position.IsBarBoundary || position.Bar < _eligibleBar) return;
        _current = _pending;
        _currentTriggerKey = _pendingTriggerKey;
        _pending = null;
    }

    private void TriggerStep(Pattern pattern, int stepIndex, ulong pulse)
    {
        foreach (var note in pattern.Steps[stepIndex].Notes)
        {
            var noteNumber = note.Pitch switch
            {
                DrumPitch drum => drum.NoteNumber,
                MelodicPitch melodic => PentatonicPitchResolver.Resolve(melodic,
                    pattern.TonalContext!.Value, pattern.Role!.Value),
                _ => throw new InvalidOperationException("Unsupported pattern pitch type.")
            };
            _output.NoteOn(_channel, noteNumber, note.Velocity);
            var duration = Math.Clamp((int)Math.Ceiling(pattern.Timing.PulsesPerStep * note.Gate.Value),
                1, pattern.Timing.PulsesPerStep);
            _scheduledNotes.Add(new ScheduledNote(_channel, noteNumber, checked(pulse + (ulong)duration)));
        }
    }

    private void ReleaseDueNotes(ulong pulse)
    {
        for (var index = _scheduledNotes.Count - 1; index >= 0; index--)
        {
            var note = _scheduledNotes[index];
            if (note.DuePulse > pulse) continue;
            try { _output.NoteOff(note.Channel, note.Note, new MidiValue(0)); }
            finally { _scheduledNotes.RemoveAt(index); }
        }
    }

    private void OnStateChanged(TransportState state)
    {
        if (state == TransportState.Running) return;
        lock (_sync)
        {
            if (_disposed) return;
            try { SilenceUnsafe(); }
            catch (Exception ex) { _error = ex; _enabled = false; }
            _lastPulse = null;
        }
    }

    private void SilenceUnsafe()
    {
        List<Exception>? errors = null;
        foreach (var note in _scheduledNotes)
        {
            try { _output.NoteOff(note.Channel, note.Note, new MidiValue(0)); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        _scheduledNotes.Clear();
        if (errors is not null) throw new AggregateException("One or more pattern notes could not be silenced.", errors);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _transport.PulseAccepted -= OnPulse;
            _transport.StateChanged -= OnStateChanged;
            try { SilenceUnsafe(); }
            finally { _disposed = true; }
        }
    }

    private readonly record struct ScheduledNote(MidiChannel Channel, MidiValue Note, ulong DuePulse);
}
