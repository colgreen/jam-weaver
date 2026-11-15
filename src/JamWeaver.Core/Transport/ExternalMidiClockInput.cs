namespace JamWeaver.Core.Transport;

public enum RealtimeMessage { Clock, Start, Continue, Stop }

public sealed class ExternalMidiClockInput
{
    public event EventHandler<RealtimeMessage>? MessageReceived;

    public void Receive(RealtimeMessage message)
    {
        if (!Enum.IsDefined(message)) throw new ArgumentOutOfRangeException(nameof(message));
        MessageReceived?.Invoke(this, message);
    }
}
