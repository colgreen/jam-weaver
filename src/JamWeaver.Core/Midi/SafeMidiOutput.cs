namespace JamWeaver.Core.Midi;

public sealed class SafeMidiOutput : IDisposable
{
    private readonly object _sync = new();
    private readonly HashSet<(MidiChannel Channel, MidiValue Note)> _activeNotes = [];
    private IMidiOutputPort? _port;
    private bool _disposed;

    public string? PortName { get { lock (_sync) return _port?.Name; } }

    public void ReplacePort(IMidiOutputPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SilenceUnsafe();
            _port?.Dispose();
            _port = port;
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
            SilenceUnsafe();
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

    private void SilenceUnsafe()
    {
        if (_port is null) { _activeNotes.Clear(); return; }
        foreach (var (channel, note) in _activeNotes)
            _port.SendNoteOff(channel, note, new MidiValue(0));
        _activeNotes.Clear();
        for (var channel = 1; channel <= 16; channel++)
            _port.SendControlChange(new MidiChannel(channel), new MidiValue(123), new MidiValue(0));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            try { SilenceUnsafe(); }
            finally
            {
                _port?.Dispose();
                _port = null;
                _disposed = true;
            }
        }
    }
}
