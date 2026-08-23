# Project summary

JamWeaver is a .NET 8 MIDI application for contributing generated patterns to
live jams. Its tested core library owns MIDI safety, clock and transport,
immutable patterns, deterministic generation, playback, candidate audition, and
JSON persistence. The console owns MIDI-device adapters and line-oriented
controls.

Jam-session preferences are ergonomic defaults, not product boundaries. The
application should start in a context that is immediately useful for its regular
sessions while keeping its musical model and controls capable of other keys,
scales, styles, roles, and devices. In particular, A minor pentatonic is the
current startup preference rather than JamWeaver's musical identity.

The application currently plays one MIDI-routed track. Patterns may contain
several notes per step and can follow internal or external MIDI Clock. Changes
made during playback take effect at the next bar. A performer can generate,
mutate, audition, accept, reject, save, and load patterns without sending
device-specific patch data.

The default Euclidean generator creates concise one-bar melodic patterns in the
selected bass, middle, or high register. A separate Euclidean2 generator tests
bounded A, A-prime, B, return development without changing that baseline.
Four-bar motif, structured-phrase, groove-vocabulary, and drum generators also
remain available for experimentation.
Generated material and recipes are deterministic and versioned; saved
materialized steps remain authoritative for playback.

The generic melodic path, note cleanup, internal and external clock, candidate
workflow, and save/restart/load have been exercised on physical devices.
Generator musical quality, quantitative timing, long-running behavior, drum
workflow, and device-specific controls still require further hardware work.

See [project structure](architecture/project-structure.md), [pattern
generation](generation/pattern-generation.md), [candidate
workflow](performance/candidate-workflow.md), and [pattern
library](persistence/pattern-library.md).
