# Project structure

The solution separates device-independent behavior from MIDI-library and
terminal concerns:

```text
src/
  JamWeaver.Core/       MIDI safety and transport abstractions
  JamWeaver.Console/    terminal UI and DryWetMIDI adapters
tests/
  JamWeaver.Core.Tests/ xUnit v3 unit tests using fake MIDI ports
  JamWeaver.Console.Tests/ deterministic console-layer generation tests
```

`JamWeaver.Core` has no dependency on DryWetMIDI or console APIs. It owns
validated MIDI values, safe output, clock and transport, immutable patterns,
deterministic generation, playback and audition state, and the pattern library.
These responsibilities remain separated by namespace so timing, generation,
persistence, and MIDI safety can be tested independently.

`JamWeaver.Console` owns port enumeration, DryWetMIDI event conversion,
command parsing, and performer-selectable generation controls. `Program` is the
composition root and owns the stable MIDI, transport, playback, and persistence
resources. `JamWeaverConsole` runs the interactive read/dispatch loop, owns its
session state and replaceable MIDI input, and keeps command failures inside the
interactive session. `CandidateGenerator` maps `GenerationControls`, a seed, and
the current candidate context to strongly typed core settings.
`ConsoleDisplay` renders setup, prompts, status, patterns, library entries, help,
and console-specific labels through an injected `TextWriter`; rendering does not
change application state. Incoming external clock/transport drives the local
engine but is never implicitly relayed to an output.

The core MIDI interface uses explicit operations such as `SendNoteOn` and
`SendClock`; it does not expose DryWetMIDI event types. Tests can therefore use a
recording fake without hardware or native MIDI access.

Build the solution with `dotnet build JamWeaver.sln`. Run tests with
`dotnet run --project tests/JamWeaver.Core.Tests` and
`dotnet run --project tests/JamWeaver.Console.Tests`.

Related: [note lifecycle](../midi/note-lifecycle.md), [clock and
transport](../midi/clock-transport.md), and [pattern
generation](../generation/pattern-generation.md).
