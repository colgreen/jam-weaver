namespace JamWeaver.ConsoleApp;

/// <summary>Owns the selected input and forwards its real-time messages to the local clock bridge.</summary>
/// <remarks>Replacement and disposal belong on the command path, never inside a MIDI callback.</remarks>
public sealed class MidiInput : IDisposable
{
    private readonly object _sync = new();
    private readonly object _forwardSync = new();
    private readonly ExternalMidiClockInput _clock;
    private IMidiInputPort? _port;
    private string? _name;
    private bool _disposed;

    public MidiInput(ExternalMidiClockInput clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public string? Name => Volatile.Read(ref _name);

    /// <summary>Takes ownership of a proposed port, including cleanup if selection or listening fails.</summary>
    public void ReplacePort(IMidiInputPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        lock (_sync)
        {
            if (ReferenceEquals(port, _port)) return;
            string? proposedName = null;
            var adopted = false;
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                proposedName = port.Name;
                ThrowCleanupErrors(CloseCurrent(), "Could not close the previous MIDI input before replacement.");
                Volatile.Write(ref _name, proposedName);
                Volatile.Write(ref _port, port);
                adopted = true;
                try
                {
                    port.MessageReceived += ForwardMessage;
                    port.StartListening();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Could not start listening to MIDI input '{proposedName}': {ex.Message}", ex);
                }
            }
            catch (Exception failure)
            {
                List<Exception> errors = [failure];
                if (adopted) errors.AddRange(CloseCurrent());
                else Attempt(port.Dispose, errors, $"dispose proposed MIDI input '{proposedName ?? "unidentified"}'");
                if (errors.Count > 1) throw new AggregateException("MIDI input selection and cleanup failed.", errors);
                throw;
            }
        }
    }

    private void ForwardMessage(object? sender, RealtimeMessage message)
    {
        lock (_forwardSync)
        {
            if (sender is not null && ReferenceEquals(sender, Volatile.Read(ref _port))) _clock.Receive(message);
        }
    }

    private List<Exception> CloseCurrent()
    {
        var port = _port;
        var name = _name;
        lock (_forwardSync)
        {
            Volatile.Write(ref _port, null);
            Volatile.Write(ref _name, null);
        }
        List<Exception> errors = [];
        if (port is null) return errors;
        Attempt(() => port.MessageReceived -= ForwardMessage, errors, $"detach messages from MIDI input '{name}'");
        Attempt(port.StopListening, errors, $"stop listening to MIDI input '{name}'");
        Attempt(port.Dispose, errors, $"dispose MIDI input '{name}'");
        return errors;
    }

    private static void Attempt(Action operation, List<Exception> errors, string context)
    {
        try { operation(); }
        catch (Exception ex) { errors.Add(new InvalidOperationException($"Could not {context}: {ex.Message}", ex)); }
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
            _disposed = true;
            ThrowCleanupErrors(CloseCurrent(), "Could not completely close the selected MIDI input.");
        }
    }
}
