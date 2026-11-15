namespace JamWeaver.ConsoleApp;

/// <summary>Receives classified real-time messages from one native MIDI input.</summary>
/// <remarks>MessageReceived uses the port instance as its sender; the owner controls listening and disposal.</remarks>
public interface IMidiInputPort : IDisposable
{
    string Name { get; }
    event EventHandler<RealtimeMessage>? MessageReceived;
    void StartListening();
    void StopListening();
}
