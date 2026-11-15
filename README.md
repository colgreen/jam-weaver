# JamWeaver

A .NET 11 console application for generating, auditioning, and playing MIDI patterns with internal or external MIDI Clock.

For architecture and runtime behavior, start with the [theory of operation](lode/architecture/theory-of-operation.md), then the [developer primer](lode/development/developer-primer.md). The [Lode map](lode/lode-map.md) indexes the subsystem contracts.

## Run

Install the .NET 11.0.100 SDK (or a newer servicing patch in its 11.0.1xx feature
band) to match `global.json`. All projects target
`net11.0`; running a framework-dependent build requires the .NET 11 runtime.

Connect the MIDI device before starting the program, then run:

```powershell
dotnet restore
dotnet run --project src/JamWeaver.Console
```

At the prompt, list and select ports:

```text
setup
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

Follow a clock received at the selected input:

```text
source external
status
```

External clock and transport are observed but deliberately not relayed, avoiding a feedback loop to the clock source. Type `help` for all commands.

Build and run both native xUnit v3 test runners using PowerShell 7 or later:

```powershell
pwsh -NoProfile -File ./verify.ps1
```

Add `-Configuration Release` to match CI. The script stops at the first failure
and returns its exit code. Build warnings are errors; tests use fake MIDI ports.
See [verification](lode/development/verification.md) for individual commands,
SDK/analyzer policy, script regression checks, and the GitHub workflow.

## Novation Circuit Tracks

The usual default channels are synth 1 on channel 1, synth 2 on channel 2, drums on channel 10, and project selection on channel 16. Enable the desired Note, CC, Program Change, and Clock receive settings on the Circuit.

For performance use, note that desktop operating systems do not offer hard real-time scheduling. The clock loop combines timed waits with a short spin wait, which is suitable for a prototype but is not a substitute for a dedicated hardware clock in timing-critical live setups.
