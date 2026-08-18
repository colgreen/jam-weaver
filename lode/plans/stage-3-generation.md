# Stage 3: deterministic generation and mutation

> Status: complete and verified by fixed-seed xUnit fixtures.

Stage 3 produces immutable melodic and drum `Pattern` snapshots from explicit
settings and seeds. It also mutates existing snapshots without scheduling or
playing them. Every result is reproducible across machines and supported .NET
versions for the same generator ID, version, settings, seed, and parent content.

## Proposed source structure

```text
src/JamWeaver.Core/Generation/
  RandomSourceExtensions.cs
  NormalizedAmount.cs
  MusicalRoleProfile.cs
  PentatonicPitchResolver.cs
  EuclideanRhythm.cs
  MelodicGeneratorSettings.cs
  MelodicPatternGenerator.cs
  DrumGeneratorSettings.cs
  DrumPatternGenerator.cs
  MutationSettings.cs
  PatternMutator.cs
```

Tests mirror these components under
`tests/JamWeaver.Core.Tests/Generation/`.

## Determinism contract

The core will reference pinned Redzen 16.0.0 and obtain seeded `IRandomSource`
instances through `RandomDefaults.CreateRandomSource(seed)`. Redzen's default is
from the xoshiro family. The application will not implement a competing PRNG or
use `System.Random`.

Generation uses the `IRandomSource` API for bounded integers and unit-interval
values, with small project helpers only for domain operations such as weighted
choices and deterministic shuffling. Generator code consumes values in an
intentional order. Changing that order, the Redzen package/default random source,
or any generation rule requires incrementing the generator version.

Version-1 IDs are `melodic-euclidean-motif`, `drum-euclidean-voices`, and `controlled-mutation`.

Fixed-seed snapshot tests assert complete materialized patterns and canonical
recipes. These tests intentionally fail if output changes without a version
increment.

## Shared controls

`NormalizedAmount` is a finite value from 0.0 through 1.0. It represents the
performer-facing dimensions:

- Density
- Movement
- Repetition
- Gate
- Velocity variation
- Mutation strength

The terminal UI can later present named choices such as low, medium, and high,
but recipes store exact normalized values. Defaults use moderate, conservative
settings and are defined in code rather than hidden in the UI.

Generated names are descriptive but not required to be unique. Every generation
and mutation result receives a new `PatternId` and a complete recipe.

## Musical role profiles

Profiles convert the friendly role into explicit constraints:

| Role | MIDI range | Hits per 16 steps | Base velocity | Character |
| --- | ---: | ---: | ---: | --- |
| Bass | 36-52 | 3-8 | 100 | Root/fifth bias, small movement, repetition |
| Middle | 48-72 | 4-12 | 88 | Wider motif and moderate variation |
| High | 67-88 | 2-8 | 76 | More space, cautious intervals, lower level |

For lengths other than 16, hit bounds scale proportionally and are clamped to
1 through the step count. Density selects an integer within the profile bounds.
Role profiles also define maximum pitch movement and safe velocity bounds.

Profiles are immutable data exposed to tests. They are defaults, not
device-specific mappings.

## Pitch resolution

Stage 3 adds the pure resolver needed to enforce role ranges. Pentatonic
intervals are 0, 2, 4, 7, 9 for major and 0, 3, 5, 7, 10 for minor.

For a tonal context and role, the resolver finds the lowest occurrence of the
root within the role range as the octave-zero anchor. `ScaleDegree`,
`OctaveOffset`, and `ChromaticOffset` resolve relative to that anchor.

Generation selects only melodic pitches whose resolved MIDI notes remain inside
the role range and MIDI 0-127. Resolution outside those bounds fails explicitly;
it does not silently clamp, because clamping can collapse distinct pitches into
duplicates.

The same resolver will later be used by playback. Enharmonic note names remain
presentation concerns.

## Euclidean rhythm

`EuclideanRhythm` distributes a requested number of hits as evenly as possible
across the requested step count. It is pure and contains no randomness.

The seeded generator chooses a rotation after constructing the rhythm. Step zero
is forced to a hit for the conservative default, unless a future explicit
off-beat setting opts out. This makes generated loops easier to locate against
the bar during audition.

The recipe records step count, derived hit count, and rotation as well as the
friendly density input. Recording derived decisions makes patterns explainable
without rerunning the generator.

## Melodic generation

`MelodicGeneratorSettings` contains:

- Pattern name, step count, and timing
- Tonal context and musical role
- Density, movement, repetition, gate, and velocity variation
- Seed

Generation is layered:

1. Derive hit count from role and density; construct and rotate a Euclidean
   rhythm.
2. Choose a motif length from 2-4 hits, biased shorter by higher repetition.
3. Build a bounded melodic walk in scale-degree space.
4. For bass, strongly weight tonic, fifth, and octave shapes; other degrees
   remain possible only as movement increases.
5. Repeat the motif across hits. Repetition controls the probability and size of
   bounded variations at motif boundaries, never independent random notes.
6. Resolve every pitch and reject choices outside the role profile.
7. Derive velocity around the role base and gate within a conservative range.

The first melodic generator is monophonic: at most one note per triggered step.
The Stage 2 model remains polyphonic for later chord generators and drum hits.
Generated step trigger probability is 1.0; probability-based playback variation
is deferred so a saved materialized loop always sounds structurally identical.

The recipe contains every input plus derived rhythm rotation and motif length.

## Drum generation

`DrumGeneratorSettings` contains:

- Pattern name, step count, timing, and seed
- One through eight distinct literal MIDI drum notes
- Per-voice density
- Shared gate and velocity variation

Each voice receives its own Euclidean rhythm and deterministic rotation. Voice
rotations are biased away from complete alignment, but simultaneous hits remain
valid. Combining voices produces multi-note steps.

No note number is assigned a semantic name such as kick or snare. The performer
or later device profile supplies the literal notes, keeping the generator usable
with Circuit, Nord Drum, Zynthian, and other MIDI devices.

The recipe records each voice note, density, hit count, and rotation using stable
indexed keys.

## Controlled mutation

`MutationSettings` contains strength and seed. Mutation accepts one materialized
melodic or drum parent and returns a new snapshot with:

- A new pattern ID
- The parent pattern ID in its recipe
- Unchanged mode, timing, tonal context, and role
- The same step count

Strength determines a bounded edit budget rather than a probability applied
independently everywhere. At low strength, one small edit is made. At maximum
strength, no more than one third of steps are directly edited in one mutation.

Eligible melodic edits are:

- Move a pitch by a small in-palette interval within the role range.
- Add, remove, or move one trigger while retaining at least one hit.
- Adjust velocity or gate within role-safe bounds.
- Rotate the pattern.

Eligible drum edits operate per voice:

- Add, remove, or move a trigger while retaining the configured voice note.
- Adjust velocity or gate.
- Rotate one voice's rhythm.

An operation is chosen only when it can produce a valid change. Mutation must
either return a pattern with different musical content or report that no valid
mutation exists; it may not silently return a new ID for identical content.

Step locking is not implemented in Stage 3 because the domain does not yet have
a lock representation. It remains an explicit later extension.

## Recipe reconstruction

Each generator defines strongly typed settings and bidirectional conversion to
the generic typed `GeneratorRecipe`. Reconstruction validates:

- Exact generator ID and supported version
- Required keys and value kinds
- No unknown keys for that version
- All musical ranges and cross-field invariants

Malformed or future-version recipes fail with contextual errors. Materialized
steps remain playable even if their recipe cannot be reconstructed in a future
version; persistence behavior is Stage 6.

## xUnit verification

Tests will cover:

- Seeded Redzen integration, bounded ranges, settings boundaries, NaN, and
  infinities, without duplicating Redzen's own PRNG tests.
- Role density calculations and pentatonic resolution across roots, palettes,
  roles, and degrees, including explicit out-of-range failure.
- Euclidean hit counts, evenness, edges, and rotations.
- Fixed-seed full snapshots for each melodic role and both palettes.
- Bass root/fifth bias as an exact versioned fixture, not a statistical test.
- Fixed-seed multi-voice drum snapshots and simultaneous hits.
- Same inputs producing equivalent content and recipes but different snapshot
  IDs; materialized content comparison ignores identity.
- Changed seeds producing changed content for representative settings.
- Mutation ancestry, edit budgets, validity, guaranteed change, and immutability.
- Recipe round trips and rejection of invalid or unsupported parameters.

Tests require no MIDI device, clock, wall-clock timing, or DryWetMIDI dependency.

## Explicitly deferred

- Playback, scheduling, candidate/accepted state, undo, and quantized swaps.
- Key-finder/UI work, persistence, and migrations.
- Chords, ties, full scales/modes, swing, ratchets, locks, and device profiles.

## Resolved decisions

- Stage 3 includes melodic and generic multi-voice drum generation, implemented
  melodic-first internally.
- Generated trigger probability is 1.0; playback randomness is deferred.
- Initial generation places a hit on step zero. Controlled mutation may rotate
  a candidate away from the downbeat.

Related: [generation design](../generation/pattern-generation.md),
[pattern model](../sequencer/pattern-model.md), and the
[initial sequencer plan](initial-sequencer.md).
