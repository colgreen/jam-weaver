using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using JamWeaver.Core.Transport;

namespace JamWeaver.ConsoleApp.DryWetMidi;

internal sealed class DryWetMidiInput : IDisposable
{
    private readonly InputDevice _device;
    private readonly ExternalMidiClockInput _input;

    public DryWetMidiInput(InputDevice device, ExternalMidiClockInput input)
    {
        _device = device;
        _input = input;
        _device.EventReceived += OnEventReceived;
        _device.StartEventsListening();
    }

    public string Name => _device.Name;

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
        if (message is { } value) _input.Receive(value);
    }

    public void Dispose()
    {
        _device.StopEventsListening();
        _device.EventReceived -= OnEventReceived;
        _device.Dispose();
    }
}
