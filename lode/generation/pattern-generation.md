# Pattern generation

Generators create intentional, repeatable musical structures rather than
choosing every note independently. For a given generator ID, version, settings,
seed, and parent pattern, the musical result is deterministic.

Random choices use the pinned Redzen package through
`RandomDefaults.CreateRandomSource(seed)`. Changing the package's default random
source, the order in which random values are consumed, or a generation rule
requires review of the generator version. Fixed-seed tests protect each
version's output contract.

## Available generators

- `melodic-musical-motif` version 3 is the default. It develops a short idea in
  the bass, middle, or high role through four related bars and selects one of
  several seed-driven rhythm variants. See [motif
  generation](motif-generation.md).
- `melodic-structured-phrase` builds one-, two-, or four-bar phrases with
  controlled rhythmic and melodic development. See [phrase
  generation](phrase-generation.md).
- `melodic-groove-vocabulary` creates four-bar bass phrases from a versioned
  rhythm vocabulary. It remains an alternative pending comparative hardware
  audition. See [groove generation](groove-generation.md).
- `melodic-euclidean-motif` is the original simple melodic generator. Its
  version-3 behavior remains available through `simple` mode.
- `drum-euclidean-voices` distributes user-supplied MIDI drum notes across
  separate Euclidean rhythms. It does not assign device-specific drum names.

## Shared controls and constraints

Performer-facing controls describe musical effects such as activity, movement,
variation, turnaround strength, role, and gate length. Recipes store the exact
resolved settings needed to explain and reproduce the result.

Melodic pitches are stored as scale degrees and resolved through the current
root, pentatonic palette, and musical role.

When no candidate supplies a tonal context, console generation starts in A
minor pentatonic (A, C, D, E, and G). Root and palette controls can then create
a candidate in a different tonal context. This is a jam-friendly startup choice,
not a requirement that generated or played music remain in A minor pentatonic.

The current implementation is nevertheless more constrained than the intended
product: generated melodic material supports all 12 roots but only major and
minor pentatonic palettes. Arbitrary scales, custom pitch collections, and a
fully chromatic generation mode are not yet supported. Future palette work
should expand these choices without weakening the useful startup default or the
deterministic recipe contract.

Default role ranges are:

| Role | MIDI range | Character |
| --- | ---: | --- |
| Bass | 36-52 | Sparse, stable, and strongly repetitive |
| Middle | 48-72 | Moderate density and wider movement |
| High | 67-88 | More space, cautious intervals, and lower velocity |

Role ranges deliberately overlap: they are musical tendencies with room for
movement, not partitions that clamp a contour at an octave boundary. Simple
version 3 biases bass motif starts toward the lower part of the range and
requires each pitch-movement decision to choose a nearby alternative when one
exists. Motif version 4 reflects contours away from a range boundary when that
preserves more distinct pitches. Phrase and groove remain version 1.

Changing root, palette, or role creates a candidate and never alters the
accepted pattern unexpectedly. Role changes preserve the tonal root and palette,
fit the existing scale-degree shape into the selected register, and are inherited
by subsequent generation. Motif, phrase, and simple generation support all
three roles; groove-vocabulary generation remains intentionally bass-only.

## Mutation and recipes

Mutation starts from materialized parent content and returns a new candidate.
It uses a bounded edit budget, preserves the parent, and records ancestry in the
new recipe. Supported operations change rhythm, notes, expression, turnaround,
or a conservative combination of them. Structured mutation protects identified
anchors; patterns without compatible generator metadata use conservative
downbeat and accent analysis instead.

Recipe reconstruction validates the exact generator ID and version, required
parameter names and types, musical ranges, and relationships between settings.
An unsupported recipe never prevents its materialized pattern from playing.

Automated tests establish determinism, range safety, reconstruction, and
structural invariants. They do not establish that generated material sounds good
in a live jam; that requires hardware audition.

Related: [pattern model](../sequencer/pattern-model.md) and [candidate
workflow](../performance/candidate-workflow.md).
