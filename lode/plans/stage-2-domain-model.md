# Stage 2: sequencer domain model

> Status: complete and verified by the xUnit suite.

Stage 2 introduces immutable, device-independent types for patterns and routing.
It does not schedule, generate, mutate, resolve pitches to MIDI, or persist JSON.
Those behaviors remain in later reviewed stages.

## Proposed source structure

```text
src/JamWeaver.Core/
  Sequencer/
    Pattern.cs
    PatternStep.cs
    PatternNote.cs
    PatternTiming.cs
    PatternIdentity.cs
    TonalContext.cs
    GeneratorRecipe.cs
    MidiRoute.cs
```

Tests mirror these topics under `tests/JamWeaver.Core.Tests/Sequencer/`.
Types use factory methods or constructors that reject invalid state immediately;
there is no separate valid/invalid lifecycle.

## Pattern identity

`PatternId` wraps a non-empty `Guid`. New generation and mutation results receive
new IDs. Loading preserves the saved ID.

`PatternName` is trimmed, must contain 1-80 characters, and is intended for a
human-facing save/recall menu. Name uniqueness is not a domain invariant.

`PatternSchemaVersion` is a positive integer. New in-memory patterns use version
1. Stage 6 will own JSON compatibility and migration behavior.

Pattern IDs use snapshot identity: the same ID always means the same musical
content. Value objects such as timing, pitch, and route use structural equality.
Renaming may preserve an ID because the name does not affect playback.

## Pattern

`Pattern` contains:

- `PatternId Id`
- `PatternName Name`
- `PatternSchemaVersion SchemaVersion`
- `PatternMode Mode` (`Melodic` or `Drums`)
- `PatternTiming Timing`
- A non-empty immutable ordered collection of `PatternStep`
- `MusicalRole? Role`
- `TonalContext? TonalContext`
- `GeneratorRecipe? Recipe`

Mode enforces the following combinations:

| Mode | Note pitch type | Role | Tonal context |
| --- | --- | --- | --- |
| Melodic | `MelodicPitch` only | Required | Required |
| Drums | `DrumPitch` only | Absent | Absent |

An empty pattern is invalid, but an individual step may be a rest. Pattern length
is 1-256 steps. The initial UI creates 16 steps by default.

The constructor defensively copies all input collections. Public collections are
read-only and cannot be cast back to mutable caller-owned lists.

## Timing

`PatternTiming` contains:

- `PulsesPerQuarterNote`, fixed to 24 for MIDI Clock in the initial product.
- `PulsesPerStep`, an integer from 1 through 96.
- `BeatsPerBar`, initially fixed to 4.

The default is six pulses per step: sixteenth notes at 24 PPQN. A pattern loop is
not required to equal one bar; arbitrary step counts allow short and polymetric
loops later. Bar boundaries remain derived from 96 incoming pulses and are not
reset merely because a pattern loops.

`PulsesPerStep` must divide the 96-pulse bar evenly. This admits common straight
and triplet resolutions while ensuring step and bar boundaries do not drift.

## Steps and notes

`PatternStep` contains:

- An immutable collection of zero or more `PatternNote` values.
- `TriggerProbability`, a finite value from 0.0 through 1.0 inclusive.

Probability belongs to the whole step: when a step does not trigger, none of its
notes play. Probability on a rest is valid but has no playback effect.

Each `PatternNote` contains:

- One pitch value.
- Velocity from 1 through 127. Zero is excluded because generated Note On events
  should not rely on MIDI's velocity-zero Note Off convention.
- Gate as a finite proportion greater than 0.0 and at most 1.0 of the step.

Gate values longer than one step and tied notes are deferred. This prevents
overlapping ownership of the same pitch in the first scheduler. Chord notes may
have different velocities and gates.

Duplicate pitches within one step are rejected because two simultaneous Note On
messages for the same channel and pitch would make note ownership ambiguous.

## Pitch

`PatternPitch` is an abstract value with two closed implementations:

- `MelodicPitch(ScaleDegree, OctaveOffset, ChromaticOffset)`
- `DrumPitch(MidiValue NoteNumber)`

`ScaleDegree` is zero-based and restricted to 0-4 for the initial five-note
pentatonic palettes. `OctaveOffset` is restricted to -4 through +4.
`ChromaticOffset` is restricted to -2 through +2 semitones. Playback-stage pitch
resolution must later reject or clamp results outside MIDI 0-127; Stage 2 cannot
perform that check without a role range and root resolution.

Drum pitches are literal MIDI note numbers 0-127 and bypass tonal resolution.

## Tonal context and musical role

`TonalContext` contains:

- `RootPitchClass`, represented as 0-11 where C is 0.
- `PitchPalette`, initially `MajorPentatonic` or `MinorPentatonic`.

The conventional note name displayed by a UI is presentation logic and is not
stored as domain identity. Enharmonic spellings therefore resolve to the same
pitch class.

`MusicalRole` is `Bass`, `Middle`, or `High`. Stage 2 records the selection but
does not assign role ranges or generate notes; those belong to Stage 3.

Changing root, palette, or role produces a new pattern value while preserving
the existing pattern until candidate acceptance is implemented in Stage 5.

## Generator recipe

`GeneratorRecipe` contains:

- Non-empty stable `GeneratorId`, for example `melodic-euclidean-motif`.
- Positive integer `GeneratorVersion`.
- Unsigned 64-bit `Seed`.
- Optional `PatternId ParentPatternId` for mutation ancestry.
- Immutable, ordinally sorted string-to-`RecipeValue` parameters.

`RecipeValue` is a closed value type with four variants: signed 64-bit integer,
finite double, Boolean, and non-null text. It does not expose raw `object` or
`JsonElement`. This keeps the core independent of JSON while retaining natural
types and canonical equality. Lists and nested objects are not initially
supported.

Stage 3 defines strongly typed generator settings, validates their musical
meaning, and converts them to and from generic recipe values. Basic recipe
typing does not make a number musically valid; for example, the relevant
generator must still reject a density outside its accepted range.

Parameter keys are non-empty, trimmed, case-sensitive identifiers containing
letters, digits, `-`, or `_`. Text values may be empty when a future generator
defines that as meaningful. Floating-point values reject NaN and infinities.
Duplicate keys are rejected using ordinal comparison.

The materialized steps remain canonical for playback; recipe equality or seed
reproduction never replaces them.

## MIDI routing

`MidiRoute` is separate from `Pattern` and contains:

- A non-empty output port name as reported by the port catalog.
- A validated one-based `MidiChannel`.

This keeps reusable musical content independent of connected hardware. The route
does not contain patch, program, CC, SysEx, or device-profile data. Stage 5 will
associate the current route with the single performance track.

Port names are runtime identifiers and may change across machines. Stage 6 may
persist a preferred route as a convenience, but recall must require resolution
against currently available ports rather than silently choosing another device.

## Immutability and update operations

Domain objects expose no mutable collections or settable properties. Intentional
changes use named methods that return new values, initially:

- `Pattern.Rename(...)`
- `Pattern.WithTonalContext(...)`
- `Pattern.WithRole(...)`
- `Pattern.WithSteps(...)`

`Rename` preserves `PatternId`; every operation that changes musical content
creates a new ID. Generation and mutation also create new IDs through their
Stage 3 services. Rejected audition candidates need not be persisted. Save-slot
overwrite is an explicit persistence operation and is not inferred from pattern
identity. Stage 2 does not add generic `with` access that could bypass
cross-property invariants.

## xUnit test matrix

Tests will cover:

- All lower and upper boundaries for IDs, names, MIDI values, step counts,
  timing, scale degree, octave, chromatic offset, velocity, gate, and probability.
- Rejection of NaN and infinity for gate and probability.
- Melodic/drum mode consistency and required role/context combinations.
- Rejection of mixed pitch types and duplicate pitches in a step.
- Defensive copying of step, note, and recipe-parameter collections.
- Structural equality of value objects and identity equality of patterns.
- Named update methods preserving identity and leaving the source unchanged.
- Recipe key validation, ordinal duplicate detection, and stable sort order.
- Recipe value type identity, finite-number validation, and structural equality.
- Route validation without requiring a physical MIDI device.
- Default 16-step/sixteenth-note construction.

Tests use xUnit v3 and require no DryWetMIDI or hardware access.

## Explicitly deferred

- MIDI note resolution from melodic pitch and tonal context.
- Generator and mutation algorithms.
- Trigger-probability decisions and gate scheduling.
- Pattern playback, bar-quantized swaps, and clock-loss handling.
- Candidate/accepted state and undo.
- JSON serialization, migration, file layout, and route preference persistence.
- UI menus and note-name presentation.
- Multiple simultaneous tracks and device profiles.

## Resolved decisions

Gate is greater than zero and at most one step. A UI value of zero removes or
mutes the note rather than storing a zero-length note. Consecutive notes retrigger;
ties and gates longer than one step are deferred.

Recipe parameters use the closed, generic `RecipeValue` model: integer, finite
number, Boolean, or text. Stage 3 adds generator-specific typed settings and
musical validation.

Pattern IDs identify exact musical snapshots. Generation, mutation,
transposition, role/context changes, and step changes create new IDs. Renaming
preserves identity; persistence overwrite is a separate explicit operation.

## Review questions

All Stage 2 questions are resolved.

Related: [pattern model](../sequencer/pattern-model.md),
[generation design](../generation/pattern-generation.md), and the
[initial sequencer plan](initial-sequencer.md).
