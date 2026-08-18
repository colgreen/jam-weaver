namespace JamWeaver.Core.Transport;

public enum RealtimeMessage { Clock, Start, Continue, Stop }

public sealed class ExternalMidiClockInput
{
    public event EventHandler<RealtimeMessage>? MessageReceived;

    public void Receive(RealtimeMessage message) => MessageReceived?.Invoke(this, message);
}
