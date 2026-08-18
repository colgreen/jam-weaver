# Project structure

The solution separates device-independent behavior from MIDI-library and
terminal concerns:

```text
src/
  JamWeaver.Core/       MIDI safety and transport abstractions
  JamWeaver.Console/    terminal UI and DryWetMIDI adapters
tests/
  JamWeaver.Core.Tests/ xUnit v3 unit tests using fake MIDI ports
```

`JamWeaver.Core` has no dependency on DryWetMIDI or console APIs. It owns
validated one-based `MidiChannel` and seven-bit `MidiValue` types,
`SafeMidiOutput`, internal clock emission, and external real-time event
classification. Its `Transport` namespace owns clock-source selection,
transport state and position, external-clock loss detection, musical timeline
boundaries, and bar-quantized swaps. Its `Sequencer` namespace owns immutable patterns, steps,
melodic/drum pitches, timing, typed generator recipes, and runtime MIDI routes.
Its `Generation` namespace owns Redzen-backed deterministic generation,
pentatonic resolution, Euclidean rhythms, structured A/A'/B/turnaround phrases,
role profiles, and legacy/targeted mutation.
Its `Performance` namespace owns deterministic step triggering, pulse-level
Note On/Off scheduling, candidate/accepted history, and melodic audition
transformations. Probability uses a prepared material-content key, and a
separate bounded history supports candidate browsing.
Its `Persistence` namespace owns the explicit JSON codec, bounded validation,
atomic filesystem library, and display metadata for saved entries.

`JamWeaver.Console` owns port enumeration, DryWetMIDI event conversion,
and command parsing. It composes generators and performance services for the
line-oriented live controls. Incoming external clock/transport drives the local engine
but is never implicitly relayed to an output.

The core MIDI interface uses explicit operations such as `SendNoteOn` and
`SendClock`; it does not expose DryWetMIDI event types. Tests can therefore use a
recording fake without hardware or native MIDI access.

Build the solution with `dotnet build JamWeaver.sln`. Run tests with
`dotnet run --project tests/JamWeaver.Core.Tests`.

Related: [note lifecycle](../midi/note-lifecycle.md),
[clock and transport](../midi/clock-transport.md), and the
[initial sequencer plan](../plans/initial-sequencer.md).
