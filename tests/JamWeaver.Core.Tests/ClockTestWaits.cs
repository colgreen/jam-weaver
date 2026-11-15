using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

internal static class ClockTestWaits
{
    public static Task ForRunCompletionAsync(InternalMidiClock clock) => WaitAsync(clock)
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static async Task WaitAsync(InternalMidiClock clock)
    {
        while (clock.IsRunning)
            await Task.Delay(1, TestContext.Current.CancellationToken);
    }
}
