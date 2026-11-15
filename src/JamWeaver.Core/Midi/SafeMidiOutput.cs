namespace JamWeaver.Core.Midi;

/// <summary>Owns the selected output and tracks notes for best-effort cleanup.</summary>
public sealed class SafeMidiOutput : IDisposable
{
    private readonly object _sync = new();
    private readonly HashSet<(MidiChannel Channel, MidiValue Note)> _activeNotes = [];
    private IMidiOutputPort? _port;
    private string? _portName;
    private bool _disposed;

    public string? PortName { get { lock (_sync) return _portName; } }

    /// <summary>Takes ownership of a proposed port, disposing it if replacement fails.</summary>
    public void ReplacePort(IMidiOutputPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        lock (_sync)
        {
            if (ReferenceEquals(port, _port)) return;
            string? proposedName = null;
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                proposedName = port.Name;
                ThrowCleanupErrors(SilenceUnsafe(), $"Could not silence MIDI output '{_portName}' before replacement.");
                var previous = _port;
                var previousName = _portName;
                _port = null;
                _portName = null;
                try { previous?.Dispose(); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Could not dispose previous MIDI output '{previousName}': {ex.Message}", ex);
                }
                _port = port;
                _portName = proposedName;
            }
            catch (Exception failure)
            {
                try { port.Dispose(); }
                catch (Exception disposalFailure)
                {
                    throw new AggregateException("MIDI output replacement and rejected-port disposal failed.", failure,
                        new InvalidOperationException($"Could not dispose proposed MIDI output '{proposedName ?? "unidentified"}': {disposalFailure.Message}", disposalFailure));
                }
                throw;
            }
        }
    }

    public async Task SendNoteAsync(MidiChannel channel, MidiValue note, MidiValue velocity,
        TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        NoteOn(channel, note, velocity);
        try { await Task.Delay(duration, cancellationToken).ConfigureAwait(false); }
        finally { NoteOff(channel, note, new MidiValue(0)); }
    }

    public void NoteOn(MidiChannel channel, MidiValue note, MidiValue velocity)
    {
        lock (_sync)
        {
            PortUnsafe.SendNoteOn(channel, note, velocity);
            _activeNotes.Add((channel, note));
        }
    }

    public void NoteOff(MidiChannel channel, MidiValue note, MidiValue velocity)
    {
        lock (_sync)
        {
            PortUnsafe.SendNoteOff(channel, note, velocity);
            _activeNotes.Remove((channel, note));
        }
    }

    public void ControlChange(MidiChannel channel, MidiValue controller, MidiValue value) =>
        WithPort(port => port.SendControlChange(channel, controller, value));

    public void ProgramChange(MidiChannel channel, MidiValue program) =>
        WithPort(port => port.SendProgramChange(channel, program));

    public void Clock() => WithPort(port => port.SendClock());
    public void Start() => WithPort(port => port.SendStart());
    public void Continue() => WithPort(port => port.SendContinue());
    public void Stop() => WithPort(port => port.SendStop());

    public void Panic()
    {
        lock (_sync)
        {
            _ = PortUnsafe;
            ThrowCleanupErrors(SilenceUnsafe(), $"Could not fully silence MIDI output '{_portName}'.");
        }
    }

    private void WithPort(Action<IMidiOutputPort> action)
    {
        lock (_sync) action(PortUnsafe);
    }

    private IMidiOutputPort PortUnsafe
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _port ?? throw new InvalidOperationException("Select a MIDI output first.");
        }
    }

    private List<Exception> SilenceUnsafe()
    {
        List<Exception> errors = [];
        if (_port is null) { _activeNotes.Clear(); return errors; }
        foreach (var (channel, note) in _activeNotes.ToArray())
        {
            try
            {
                _port.SendNoteOff(channel, note, new MidiValue(0));
                _activeNotes.Remove((channel, note));
            }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException(
                    $"MIDI output '{_portName}' could not send Note Off for channel {channel.Number}, note {note.Value}: {ex.Message}", ex));
            }
        }
        for (var channel = 1; channel <= 16; channel++)
        {
            try { _port.SendControlChange(new MidiChannel(channel), new MidiValue(123), new MidiValue(0)); }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException(
                    $"MIDI output '{_portName}' could not send All Notes Off (CC 123) for channel {channel}: {ex.Message}", ex));
            }
        }
        return errors;
    }

    private static void ThrowCleanupErrors(List<Exception> errors, string message)
    {
        if (errors.Count > 0) throw new AggregateException(message, errors);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            var errors = SilenceUnsafe();
            var port = _port;
            var name = _portName;
            _port = null;
            _portName = null;
            _activeNotes.Clear();
            _disposed = true;
            try { port?.Dispose(); }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException($"Could not dispose MIDI output '{name}': {ex.Message}", ex));
            }
            ThrowCleanupErrors(errors, $"Could not completely close MIDI output '{name}'.");
        }
    }
}
