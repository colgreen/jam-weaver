# Pattern model

> Implementation status: the domain model, generation, playback, and versioned
> JSON persistence are implemented and covered by xUnit tests.

A pattern is a deterministic, persistable step sequence for one MIDI-routed
track. The initial product has one track, but a step can hold several notes so
the model supports chords and simultaneous drum voices without redesign.

## Pattern

A pattern records:

- Stable identifier, user-visible name, and persistence format version.
- Mode: melodic or drums.
- Length and resolution; the default is 16 sixteenth-note steps in 4/4.
- Ordered materialized steps for exact playback.
- Generator recipe and seed when the pattern was generated.
- Musical role for melodic material: bass, middle, or high.
- Accepted key/palette used when the pattern was last auditioned.

Resolution and length are data rather than hard-coded playback assumptions.
The initial UI can expose only the 16-step default while preserving room for
shorter loops and other resolutions.

## Step

Each step records:

- Zero or more note events.
- Velocity from 1 through 127 for each note.
- Gate as a proportion of the step duration.
- Trigger probability from zero through one.

Generated patterns do not initially contain CC, program-change, patch, or
effects automation. Sound design remains under the performer's hands on the
target hardware.

For melodic patterns, a note stores scale degree, octave displacement, and an
optional chromatic alteration. Playback resolves it through the current key and
palette. This preserves the melodic shape when auditioning another key.

For drum patterns, a note stores an absolute MIDI note number. Different drum
voices conventionally occupy different note numbers and can trigger together.

## Persistence

Patterns use a human-readable, versioned JSON format. A generated pattern saves
both:

- Its materialized steps, guaranteeing exact recall.
- Its recipe: generator identifier and version, parameters, seed, and parent
  pattern identifier for mutation.

Saving only a seed is insufficient because generator algorithms can evolve and
mutation depends on its starting material. Materialized steps remain canonical
for playback; the recipe enables reproduction, explanation, and further
mutation.

MIDI output port and channel are performance routing, not inherent musical
content. Persistence may remember a preferred route later, but loading a pattern
must not silently select a patch or send device-specific messages.

The implemented format uses one indented UTF-8 JSON file per pattern in the
working-directory `patterns` library. It has a versioned envelope, explicit
pitch and recipe-value discriminators, decimal-string 64-bit seeds, a 1 MiB
read limit, domain validation on load, and atomic same-directory replacement.
Unknown properties are tolerated; unknown format/schema versions are rejected.

## Identity and transformation

A pattern ID identifies an exact musical snapshot. Renaming preserves the ID;
changing steps, role, or tonal context returns a new pattern with a new ID. A
manual musical transformation clears the previous generator recipe because that
recipe no longer reproduces the transformed snapshot. Stage 3 generation and
mutation services attach a new recipe with exact ancestry.

Recipe parameters use typed integer, finite-number, Boolean, or text values.
They are stored in ordinal key order; generator-specific validation belongs to
Stage 3.

Related: [pattern generation](../generation/pattern-generation.md) and
[candidate workflow](../performance/candidate-workflow.md).
