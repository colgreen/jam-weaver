# Project summary

JamWeaver is a .NET 8 MIDI application for contributing generated patterns to
live jams. Its tested core library owns MIDI safety, clock and transport,
immutable patterns, deterministic generation, playback, candidate audition, and
JSON persistence. The console owns MIDI-device adapters and line-oriented
controls. Its command loop uses an injectable port catalog, while rendering
receives device-name lists. Discovery errors leave commands available; automated
console tests use fake ports and discovery without accessing native MIDI.

Jam-session preferences are ergonomic defaults, not product boundaries. The
application should start in a context that is immediately useful for its regular
sessions while keeping its musical model and controls capable of other keys,
scales, styles, roles, and devices. In particular, A minor pentatonic is the
current startup preference rather than JamWeaver's musical identity.

The application currently plays one MIDI-routed track. Patterns may contain
several notes per step and can follow internal or external MIDI Clock. Changes
made during playback take effect at the next bar; a fresh MIDI Start plays any
waiting candidate from the restarted first pulse. A performer can generate,
mutate, audition, accept, reject, save, and load patterns without sending
device-specific patch data.

The default Euclidean generator creates concise one-bar melodic patterns in the
selected bass, middle, or high register. A separate Euclidean phrase generator tests
bounded A, A-prime, B, return development without changing that baseline.
The Turnaround Loop generator repeats a concise Euclidean source bar in the
form A, A, A, and A-turnaround. Its generator-specific variation preserves that
identity through mirrored loop edits and isolated final gestures.
Four-bar motif, structured-phrase, groove-vocabulary, and drum generators also
remain available for experimentation.
Generated material and recipes are deterministic and versioned; saved
materialized steps remain authoritative for playback. Each generator owns both
generation and variation, and mutation recipes retain the original generator
lineage so repeated variation returns to the same generator.

The generic melodic path, note cleanup, internal and external clock, candidate
workflow, and save/restart/load have been exercised on physical devices.
The current console path has [external-clock smoke
coverage](hardware/external-clock-validation.md) for discovery, playback,
Stop/restart, live candidate replacement, clock-loss silence, paused behavior
after pulses return, recovery through Stop/Start, and clean shutdown.
Generator musical quality, quantitative timing, long-running behavior, drum
workflow, and device-specific controls still require further hardware work.

See [theory of operation](architecture/theory-of-operation.md),
[project structure](architecture/project-structure.md), [pattern
generation](generation/pattern-generation.md), [candidate
workflow](performance/candidate-workflow.md), and [pattern
library](persistence/pattern-library.md).
