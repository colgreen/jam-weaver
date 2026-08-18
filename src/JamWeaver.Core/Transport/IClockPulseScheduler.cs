using System.Diagnostics;

namespace JamWeaver.Core.Transport;

public interface IClockPulseScheduler
{
    Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken);
}

public sealed class StopwatchClockPulseScheduler : IClockPulseScheduler
{
    public async Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var next = stopwatch.Elapsed.TotalSeconds;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pulse();
            next += 60.0 / (bpm() * 24.0);
            var remaining = next - stopwatch.Elapsed.TotalSeconds;
            if (remaining > .002)
                await Task.Delay(TimeSpan.FromSeconds(remaining - .001), cancellationToken).ConfigureAwait(false);
            while (stopwatch.Elapsed.TotalSeconds < next)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.SpinWait(20);
            }
        }
    }
}
