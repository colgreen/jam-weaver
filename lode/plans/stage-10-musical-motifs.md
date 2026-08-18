# Stage 10: musical motif grammar

> Status: implemented and automatically verified; musical quality awaits hardware audition.

Stage 10 adds a deliberately low-entropy melodic generator after phrase and
groove generation remained too mechanical in informal listening. It creates a
small musical idea and develops it, rather than choosing each note independently.

## Product behavior

`motif` is the console startup default. Existing `phrase`, `groove`, and `simple`
generators remain available for comparison. The first implementation is bass-only
because simple bass loops are the primary live use and provide a clear audition.

Controls are:

```text
generator motif
shape auto|pedal|root-fifth|walking|call-response|arch|pickup|riff
activity sparse|medium|busy
movement low|medium|high
variation low|medium|high
generate [seed]
```

`help <command>` explains each terminal command's syntax, options, defaults, and
effect on candidates or live playback; bare `help` remains the compact index.

`auto` deterministically chooses one of the seven named archetypes. Omitting a
seed creates and prints a fresh seed; supplying one reproduces the material.

## Musical grammar

Each archetype supplies a short contour and rhythm rather than probability
weights:

- Pedal repeats the root with restrained departure.
- Root-fifth establishes stable bass motion.
- Walking uses a short adjacent-degree fragment.
- Call-response answers a small rising/falling idea.
- Arch rises to one point and returns.
- Pickup places deliberate motion near the loop boundary.
- Riff repeats a compact asymmetric cell.

The motif normally contains three to five notes and begins on the root. Movement
scales its contour without changing the archetype. All pitches remain in the
selected pentatonic palette and bass MIDI range.

Four bars use A/A'/B/return form:

- Low variation repeats A as A' exactly.
- Medium changes only A's ending, then rotates or reverses the motif for B.
- High makes one additional B change.
- The last bar returns to a stable root/fifth-class ending and may expose a pickup.

Rhythm development follows the same conservative levels. Pitch, rhythm, and
expression are materialized deterministically. Every trigger has probability
one; ghost notes and note-by-note velocity randomness are intentionally absent.
Velocity and gate express anchors, motif restarts, B emphasis, and the ending.

## Persistence and verification

Generator ID is `melodic-musical-motif`, version 1. Recipes store requested and
resolved shape, controls, tonal context, four onset masks, motif length,
development identity, and structural/ghost masks. Reconstruction and existing
JSON persistence reproduce material exactly without a schema change.

xUnit tests cover all archetypes, deterministic output, activity ordering,
exact low-variation repetition, bounded A' change, bass-range resolution,
always-on triggers, recipe reconstruction, JSON round-trip, and role rejection.

Automated rules define safety and compositional intent, not musical quality.
Audition fixed seeds on the Circuit or Zynthian before tuning contours or making
claims that one archetype works well in a jam.

Related: [generation design](../generation/pattern-generation.md), [Stage 8
phrases](stage-8-phrase-generation.md), and [Stage 9 grooves](stage-9-groove-vocabulary.md).
