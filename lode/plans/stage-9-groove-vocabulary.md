# Stage 9: groove vocabulary and perceptual variation

> Status: implemented and automatically verified; hardware audition remains.

Stage 9 tests whether rhythm vocabulary and perceptually targeted variation sound
less mechanical than Stage 8's weighted procedural rhythms. It adds an opt-in
`groove` generator alongside `phrase` and `simple`; it does not replace the
current default until hardware comparison supports that decision.

## Scope and hypothesis

The first experiment targets melodic bass because sparse bass patterns are the
most common live use and expose mechanical rhythm clearly. Middle/high material
continues to use the phrase generator; the template representation is designed
to generalize later without pretending one vocabulary suits every role.

The hypothesis is:

> Selecting and transforming a proven rhythmic skeleton to controlled
> perceptual distances, then coupling pitch and expression to that skeleton,
> produces more intentional loops than independently weighting step positions.

Success is determined by repeatable hardware audition, not automated metrics
alone. Metrics constrain and explain candidates; they are not claims of musical
quality.

## Proposed source structure

```text
src/JamWeaver.Core/Generation/Groove/
  GrooveTemplate.cs
  GrooveVocabulary.cs
  RhythmMetrics.cs
  RhythmVariationSearch.cs
  GrooveGeneratorSettings.cs
  MelodicGrooveGenerator.cs
  GroovePatternDisplay.cs
```

Templates and algorithms remain device-independent and deterministic through
Redzen. The console only parses friendly controls and displays prepared results.

## Versioned factory vocabulary

Version 1 contains twelve original, project-authored 16-step bass skeletons.
They are generic rhythmic cells, not transcriptions. Working categories are:

- Foundation: strong downbeat with restrained support.
- Offbeat pulse: stable anchor plus repeated offbeats.
- Anticipation: notes lead into stronger metrical positions.
- Long-short: alternating space and compact response.
- Sparse answer: two related half-bar statements.
- Broken pulse: irregular positions with bounded gaps.

Each category has two templates. Stable IDs such as `foundation-1` become recipe
contracts; changing a template's masks requires a vocabulary version increment.

A `GrooveTemplate` contains:

- Required-onset 16-bit mask.
- Optional-onset mask.
- Movable-onset mask, which is a subset of sounding positions.
- Two-bit accent level per step: ghost, light, normal, strong.
- Entry suitability and turnaround suitability from 0-2.
- Category and stable ID.

Masks must not overlap illegally, required step zero is the bass default, and
every template passes density, maximum-gap, and accent validation at startup.

## Rhythm metrics

`RhythmMetrics` returns a versioned immutable feature vector:

- Hit count and density.
- Weighted syncopation.
- Downbeat strength.
- Maximum circular rest gap.
- Maximum adjacent-onset cluster.
- First-half/second-half activity balance.
- Hamming distance from a reference mask.
- Directed circular onset-movement distance from a reference.

The project syncopation metric uses an explicit 16-step metrical-strength table.
An onset contributes when it anticipates a stronger silent position; its score is
the strength difference. This is documented as the project's metric rather than
presented as a universal perceptual model.

Hamming distance measures changed positions. Directed movement distance finds
the minimum circular step movement needed to match source onsets to target
onsets, plus an explicit add/remove penalty when hit counts differ. Sixteen-step
patterns permit an exact bounded assignment calculation rather than a heuristic.

## Friendly controls

```text
generator groove
groove auto|foundation|offbeat|anticipation|long-short|sparse-answer|broken
similarity close|related|contrast
activity sparse|medium|busy
variation low|medium|high
turnaround none|subtle|strong
```

Existing tonal context, role, movement, seed, gate safety, candidate workflow,
and persistence contracts remain. `generator groove` initially requires bass
and explains that middle/high should use `generator phrase`.

`auto` selects a template category deterministically. Similarity maps to target
metric bands rather than a raw edit count:

- Close: recognizable small movement, normally one onset operation.
- Related: several changes within a bounded distance window.
- Contrast: larger B-bar distance while retaining required anchors and density.

## Deterministic variation search

For each derived bar, search produces 64 candidates from the source template:

1. Retain required onsets.
2. Move, add, or remove only positions allowed by template masks and settings.
3. Reject candidates outside role/activity hit bounds, gap/cluster limits, or the
   requested distance window.
4. Score survivors against target density, syncopation, similarity, activity
   balance, and phrase intention.
5. Resolve ties through a deterministic mask ordering after seeded score terms.

This is bounded generate-and-rank search, not a genetic algorithm. It adopts the
useful target-distance idea without introducing populations, generations, or
opaque convergence behavior. If no candidate satisfies every band, the search
relaxes one documented secondary constraint in a fixed order; required anchors
and safety bounds never relax.

Four bars use different targets:

- A: selected factory skeleton with activity adjustment.
- A': Close to A; preserve all required and most ordinary onsets.
- B: Related or Contrast according to setting, retaining identity anchors.
- Turnaround: changes concentrated in steps 8-15 with suitable exit behavior.

One/two-bar support is deferred for this experiment so comparison always uses
the exact four-bar form.

## Rhythm-aware motif events

The generator no longer creates a pitch list independently of rhythm. A motif is
an ordered set of events containing:

- Relative onset within its bar.
- Scale-degree interval from the preceding event.
- Articulation class.
- Accent relation to the rhythmic template.

A maps the motif onto the selected skeleton. A' transforms the motif as a unit:
omit an optional event, displace a movable onset with its pitch, or change only
the answer/ending. B chooses an explicit response transformation. The turnaround
prefers a stable final degree or pickup toward A.

Pitch rules retain pentatonic context, role range, stable-degree bass bias,
bounded leaps, and maximum immediate repetition. When a rhythmic onset moves,
its pitch and articulation move with it rather than being reassigned by hit
ordinal; this preserves motif identity.

## Coordinated expression envelopes

Each factory template supplies accent levels, which are transformed with the
rhythm. Phrase intention then adjusts the entire envelope:

- Establish: clear first anchor, restrained later accents.
- Reinforce: similar envelope with one changed emphasis.
- Answer/intensify: a small directed velocity rise or shorter gates.
- Release/pickup: longer stable ending or increasing pickup accents.

Velocity and gate are derived from role base, accent, neighboring rest space,
and intention. There is no independent per-note velocity randomness. Optional
ghost probability remains deterministic and sparse.

Microtiming and swing remain deferred. First we determine whether better musical
material and correlated expression solve enough of the problem on the existing
quantized playback engine.

## Recipe and display

Generator ID is `melodic-groove-vocabulary`, version 1. Recipe records:

- Vocabulary/metric versions and template ID.
- Friendly controls and seed.
- Input context and role.
- A/A'/B/turnaround onset and accent masks.
- Feature vectors, target bands, relaxation used, and motif transformation IDs.

Materialized steps remain canonical. Existing JSON requires no schema change.

`pattern` retains the X/x/g/. grid and adds one compact line per bar:

```text
A' hits=6 sync=4 gap=4 distance=2 movement=2
```

`compare` alternates between the latest Stage 8 phrase and groove candidate
through candidate history. Both use the same seed, context, role, and high-level
settings where meaningful. Changes remain next-bar quantized while running.

## xUnit verification

Tests cover:

- Exact factory template IDs/masks and vocabulary validation.
- Metric fixtures for empty, straight, offbeat, anticipated, and rotated masks.
- Exact directed movement and add/remove penalties.
- Fixed-seed full snapshots for every template category.
- Deterministic 64-candidate search, tie-breaking, and relaxation reporting.
- A' and B landing inside requested feature/distance bands across fixed seeds.
- Required-anchor preservation, hit/gap/cluster bounds, and non-empty bars.
- Motif events moving with onsets and transformations retaining identity.
- Role pitch range, leap/repetition limits, and stable ending behavior.
- Accent/gate envelopes following phrase intentions without independent noise.
- Recipe reconstruction and JSON round-trip with no schema migration.
- `compare` preserving accepted state and using existing quantized playback.

Snapshot and invariant tests define algorithm contracts, not groove quality.

## Hardware audition gate

Use the Circuit or Zynthian at fixed tempo with matched seeds. For each of at
least eight seeds, audition Stage 8 phrase then Stage 9 groove without seeing the
generator label where practical. Record only coarse preference: phrase, groove,
or neither, plus “too sparse/busy/repetitive/random.”

Stage 9 becomes the default only if groove is consistently preferred. Otherwise
retain it as an alternative and use the observations to tune templates/metrics.
No claim is based on a single appealing seed.

## Explicitly deferred

- Swing, microtiming, ratchets, and timing-template playback changes.
- Learning from accepted patterns or imported MIDI.
- Markov/variable-order models and neural generation.
- Middle/high groove vocabularies and drum/bass interlocking.
- User editing of factory templates and style/genre labels.

## Research basis
- Target distance and rhythm variation: [Ó Nuanáin, Herrera, and Jordà](https://mtg.upf.edu/node/3259).
- Hierarchical rhythm/contour separation: [MusicFrameworks](https://arxiv.org/abs/2109.00663).
- Theme repetition with plausible variation: [Theme Transformer](https://arxiv.org/abs/2111.04093).
- Separate skeleton and performance layers: [PocketVAE](https://arxiv.org/abs/2107.05009).
- Bass/drum feature spaces and interlocking: [Drums and Bass Interlocking](https://mtg.upf.edu/node/3913).
Related: [Stage 8](stage-8-phrase-generation.md), [generation
design](../generation/pattern-generation.md), and [candidate
workflow](../performance/candidate-workflow.md).
