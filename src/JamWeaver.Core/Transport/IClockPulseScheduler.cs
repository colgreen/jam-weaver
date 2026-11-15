using System.Diagnostics;

namespace JamWeaver.Core.Transport;

/// <summary>Emits pulses until cancelled and completes only after all pulse callbacks have finished.</summary>
public interface IClockPulseScheduler
{
    Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken);
}

/// <summary>Keeps absolute pulse deadlines and yields between overdue pulses without skipping them.</summary>
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
            else if (remaining <= 0)
                await Task.Yield();
            while (stopwatch.Elapsed.TotalSeconds < next)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.SpinWait(20);
            }
        }
    }
}
