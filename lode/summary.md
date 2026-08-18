# Project summary

JamWeaver is a .NET 8 MIDI application intended to grow into a reliable live-jamming
instrument. The console enumerates MIDI ports, sends notes, CC and program
changes, generates MIDI Clock, and observes external clock/transport without
automatically relaying it. Device-independent MIDI output and transport live in
a tested core library; DryWetMIDI is isolated in the console infrastructure.
The planned first sequencer is a single multi-note step track
with deterministic algorithmic generation, mutation, bar-quantized changes,
and reusable saved patterns. Its accepted design is documented in the
[initial sequencer plan](plans/initial-sequencer.md). MIDI infrastructure and
the immutable pattern model, deterministic melodic/drum generation, role
profiles, controlled mutation, shared internal/external transport state,
clock-loss handling, timeline boundaries, and bar-quantized swaps are complete.
Deterministic pattern playback, pulse-based gates, candidate audition,
accept/reject/undo, and by-ear root/palette/role controls are also complete.
Accepted patterns can be atomically saved to and recalled from a versioned,
validated JSON library. The original Circuit melodic path, internal/external
clock, clock-loss cleanup, candidate workflow, and save/restart/recall have been
hardware-validated. Zynthian also confirms generic raw-note and multi-bar
melodic playback. The initial seven-stage sequencer scope is complete, with
quantitative timing, Nord Drum workflow, and device-specific controls remaining
outside the validated scope. A structured phrase generator now adds deterministic
four-bar A/A'/B/turnaround form, targeted mutation, friendly complexity controls,
ghost-note variation, candidate history, and terminal visualization; its musical
quality awaits live audition. An opt-in bass groove generator adds a versioned twelve-template rhythmic
vocabulary, perceptual-distance metrics, bounded A/A'/B/turnaround variation,
rhythm-coupled pitch and expression, recipe reconstruction, and matched phrase
comparison. It is automatically verified but awaits comparative hardware audition.
The startup default is now a lower-entropy bass motif grammar: seven named phrase
shapes build a small idea and conservatively develop it through four bars without
probabilistic notes. Its compositional invariants are tested; musical quality still
requires live hardware audition. The terminal provides a compact `help` index and
focused `help <command>` syntax, options, defaults, and live-playback effects.
See the [project structure](architecture/project-structure.md),
[MIDI safety](midi/note-lifecycle.md), and [project practices](practices.md).
