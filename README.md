# JamWeaver

A small .NET 8 console application that sends MIDI messages and can either generate MIDI clock or follow and relay an external clock.

## Run

Connect the MIDI device before starting the program, then run:

```powershell
dotnet restore
dotnet run --project src/JamWeaver.Console
```

At the prompt, list and select ports:

```text
devices
out 0
in 0
```

Generate a 120 BPM clock and play a note on MIDI channel 1:

```text
source internal
bpm 120
start
note 1 60 100 500
cc 1 74 96
stop
```

Follow a clock received at the selected input and relay clock/transport to the selected output:

```text
source external
status
```

External clock and transport are observed but deliberately not relayed, avoiding a feedback loop to the clock source. Type `help` for all commands.

Run the xUnit v3 test suite with its native runner:

```powershell
dotnet run --project tests/JamWeaver.Core.Tests
```

## Novation Circuit Tracks

The usual default channels are synth 1 on channel 1, synth 2 on channel 2, drums on channel 10, and project selection on channel 16. Enable the desired Note, CC, Program Change, and Clock receive settings on the Circuit.

For performance use, note that desktop operating systems do not offer hard real-time scheduling. The clock loop combines timed waits with a short spin wait, which is suitable for a prototype but is not a substitute for a dedicated hardware clock in timing-critical live setups.
