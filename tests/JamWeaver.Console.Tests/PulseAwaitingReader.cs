namespace JamWeaver.Console.Tests;

/// <summary>Waits for a background pulse after startup commands before advancing a scripted session.</summary>
internal sealed class PulseAwaitingReader(string commands, Task pulsed) : StringReader(commands)
{
    private string? _previous;

    public override async Task<string?> ReadLineAsync()
    {
        if (_previous is "start" or "go")
            await pulsed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _previous = await base.ReadLineAsync();
        return _previous;
    }
}
