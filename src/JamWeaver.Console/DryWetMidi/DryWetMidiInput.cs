using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace JamWeaver.ConsoleApp.DryWetMidi;

internal sealed class DryWetMidiInput : IMidiInputPort
{
    private readonly InputDevice _device;

    public DryWetMidiInput(InputDevice device) =>
        _device = device ?? throw new ArgumentNullException(nameof(device));

    public event EventHandler<RealtimeMessage>? MessageReceived;
    public string Name => _device.Name;

    public void StartListening()
    {
        _device.EventReceived += OnEventReceived;
        _device.StartEventsListening();
    }

    public void StopListening()
    {
        try { _device.StopEventsListening(); }
        finally { _device.EventReceived -= OnEventReceived; }
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        var message = e.Event switch
        {
            TimingClockEvent => RealtimeMessage.Clock,
            StartEvent => RealtimeMessage.Start,
            ContinueEvent => RealtimeMessage.Continue,
            StopEvent => RealtimeMessage.Stop,
            _ => (RealtimeMessage?)null
        };
        if (message is { } value) MessageReceived?.Invoke(this, value);
    }

    public void Dispose()
    {
        try { _device.EventReceived -= OnEventReceived; }
        finally { _device.Dispose(); }
    }
}
