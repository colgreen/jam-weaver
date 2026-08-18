# Stage 8: structured phrase generation

> Status: implemented and covered by the xUnit suite; subjective hardware
> audition of generated phrase quality remains.

Stage 8 addresses repetitive rhythm, pitch, and one-bar form with a new melodic
phrase generator. It preserves the existing generator, recipes, persisted files,
and `generate <seed>` behavior through an explicit `simple` mode while making a
structured four-bar generator the new default.

The new default phrase has 16 sixteenth-note steps per bar and one, two, or four
bars. Four bars use:

```text
A | A' | B | turnaround
```

- A establishes the rhythmic and melodic identity.
- A' varies A without obscuring it.
- B provides controlled contrast.
- The turnaround either creates space or leads clearly back to A.

Complexity comes from related development, accents, articulation, and sparse
decorations rather than independent random notes.

## Compatibility

The existing `melodic-euclidean-motif` generator/version and snapshot tests are
untouched. Its console mode is `simple`. The new generator uses ID
`melodic-structured-phrase`, version 1, because it is a separate algorithm rather
than a compatible revision.

Existing pattern schema version 1 already supports 64 steps, per-step
probability, velocity, and gate. No persistence migration or playback model
change is required. New recipe parameters use the existing typed recipe format.

## Source structure

```text
src/JamWeaver.Core/Generation/Phrase/
  PhraseGeneratorSettings.cs
  MelodicPhraseGenerator.cs
  PhrasePatternMutator.cs
src/JamWeaver.Core/Performance/
  CandidateHistory.cs
  PatternTriggerKey.cs
```

Generation remains independent of MIDI, transport, and console APIs.

## Friendly settings

The session holds settings used by later `generate` commands:

- Length: 1, 2, or 4 bars; default 4.
- Activity: sparse, medium, or busy; default medium.
- Rhythm: steady, syncopated, or broken; default syncopated.
- Movement: low, medium, or high; default medium.
- Variation: low, medium, or high; default medium.
- Turnaround: none, subtle, or strong; default subtle.
- Current tonal context and role, initially C minor pentatonic and bass.

Enums map to explicit numeric thresholds stored alongside their friendly names
in the recipe. Hidden defaults for gate and velocity spread remain conservative
but are recorded. Role profiles cap density and motion so `busy` bass remains
less dense than `busy` middle material.

## Rhythm construction

Rhythm is built hierarchically:

1. Build an A-bar skeleton with role/activity hit bounds.
2. Mark a small subset as structural anchors. Bass normally anchors step zero;
   other strong positions are seed- and character-dependent.
3. Choose ordinary and decorative hits from weighted step positions without
   replacement. `steady` favors beat-aligned positions, `syncopated` favors
   offbeats around anchors, and `broken` favors irregular spacing while enforcing
   maximum silence and cluster limits.
4. Derive A' by retaining every structural anchor and applying a bounded number
   of moves/additions/removals to other hits.
5. Derive B with greater contrast while retaining at least one identity anchor.
6. Derive the turnaround from its setting: unchanged/space for none, a small
   pickup or changed ending for subtle, and a bounded fill or deliberate final
   rest for strong.

No bar is an unconstrained fresh rhythm. Every bar has at least one sounding
step, density stays within role limits, and long gaps/clusters are bounded by
the selected character.

## Motif and phrase development

The generator constructs a 3-6-hit motif from valid pentatonic pitches. It uses
a bounded scale-degree walk with role-specific preferences:

- Bass strongly favors stable root/fifth-like degrees, low movement, and small
  leaps.
- Middle permits broader contour and more varied endings.
- High retains more space and cautious wider motion.

A maps its hits through the motif. Later bars choose transformations according
to Variation and Movement:

- Exact repetition.
- Changed final note.
- One-degree displacement of a bounded subset.
- Partial contour reversal.
- Short answer phrase.
- Slight register displacement when every resolved pitch remains in range.

A' must retain a configurable majority of A's pitch/rhythm identity. B must
differ but share at least one motif element. The turnaround prefers a stable
degree or pickup into A. Consecutive identical pitches and large resolved MIDI
leaps have role-specific maxima.

Each generated hit is classified during generation as structural, ordinary,
pickup, ghost, or phrase-ending. The classification controls materialized data:

- Structural hits: probability 1, stronger velocity, normal gate.
- Ordinary hits: probability 1, moderate expression variation.
- Pickups: probability 1, lighter velocity and shorter gate.
- Ghosts: probability in a conservative role-specific range below 1.
- Phrase endings: longer gate or intentional following space.

Structural hits form the repeatable foundation. At most a small role/activity
bounded fraction are ghosts, so loop-to-loop change is decorative rather than a
different bass line.

The existing trigger decision currently keys intermediate probability by random
pattern ID. Stage 8 replaces that with a prepared deterministic content key,
computed off the timing path from materialized musical content. Equivalent
fixed-seed generation therefore produces the same ghost-note decisions despite
different snapshot IDs. Start repeats the sequence; Continue preserves it.

Recipe parameters record friendly inputs plus derived structural, decorative,
and bar-role masks as fixed-width hexadecimal strings. These masks explain and
reconstruct generator output but do not become playback requirements.

## Targeted mutation

`PhrasePatternMutator` uses generator ID `structured-phrase-mutation`, version 1.
It accepts target and strength:

- Rhythm: move/add/remove only non-structural triggers within density/gap rules.
- Notes: alter bounded non-anchor motif notes within role and leap rules.
- Expression: alter velocity, gate, and ghost probability without moving hits.
- Turnaround: edit only the final bar while preserving a valid loop return.
- All: split a bounded edit budget across applicable targets.

Mutation always preserves step count, timing, mode, context, role, at least one
hit per bar, and parent ancestry. When the parent has a supported phrase recipe,
its masks guide protection. For transformed or legacy melodic patterns without
phrase metadata, conservative analysis protects step zero, strong downbeats,
and the most accented repeated pitches. Unsupported drum targets continue to
use the existing mutator.

## Candidate history

`CandidateHistory` stores up to eight requested candidate snapshots separately
from Accepted/PreviousAccepted. Adding a new candidate after navigating backward
discards the forward branch. Eviction removes the oldest entry but never alters
Accepted or persistence.

Commands are:

```text
generate [seed]
previous
next
```

`generate` uses current settings and a new printed Redzen-derived seed when its
optional seed is omitted; supplying a seed reproduces the same material.
Previous/next queue the selected candidate through the existing immediate or
next-bar path. Accept/reject/undo semantics remain unchanged.

## Console controls

```text
generator phrase|simple
length 1|2|4
activity sparse|medium|busy
rhythm steady|syncopated|broken
movement low|medium|high
variation low|medium|high
turnaround none|subtle|strong
generate [seed]
mutate rhythm|notes|expression|turnaround|all [seed] [strength]
previous | next
```

The existing `mutate [seed] [strength]` form remains the legacy all-purpose
mutation for compatibility. `settings` prints current phrase controls.

`pattern` adds a compact four-line hit display. `X` is a strong/structural hit,
`x` is ordinary, `g` is probabilistic/ghost, and `.` is rest. When recipe masks
are unavailable, display classification is inferred from probability and
velocity and is labeled approximate.

## xUnit verification

The Stage 8 tests cover:

- Fixed-seed full snapshot hashes for every role and rhythm character.
- Same inputs/seed producing identical material and recipes with different IDs.
- Existing generator and persistence fixtures remaining content compatible.
- Exact 1/2/4-bar lengths, recipe reconstruction, and per-bar non-empty
  invariants.
- Role range, downbeat, and A' rhythmic identity retention across a fixed matrix
  of roots, palettes, roles, and seeds.
- Structural probability 1, bounded ghost count/probability, and expression
  ranges.
- Content-key trigger decisions matching across equivalent different-ID
  patterns and restarting deterministically on Start.
- Targeted mutations changing content while respecting structural protection,
  dimension boundaries, ancestry, and turnaround scope.
- Candidate history capacity, branching, and navigation.

Tests use fixed seeds and musical invariants, never subjective claims or flaky
statistical thresholds. Hardware audition follows implementation because output
quality still requires human judgment.

- Swing, microtiming, ratchets, ties beyond adjacent full gates, and tempo-aware
  articulation.
- Chords, chord progressions, full scales/modes, and automatic key detection.
- Persisted UI settings, named style presets, and manual per-step locks.
- Drum phrase generation and device-specific drum maps.
- More than one simultaneous application track.

## Resolved decisions

- Four-bar A/A'/B/turnaround is the structured default; one/two bars remain.
- The old generator remains reproducible as explicit `simple` mode.
- Rhythm, pitch, and expression vary through related phrase transformations,
  with only sparse deterministic ghost variation between phrase repetitions.
- Friendly categorical controls drive exact recipe parameters.
- Mutation targets musical dimensions and protects structural identity.
- Candidate browsing is distinct from accepted-pattern undo.

Related: [generation design](../generation/pattern-generation.md),
[candidate workflow](../performance/candidate-workflow.md),
[pattern model](../sequencer/pattern-model.md), and
[initial sequencer plan](initial-sequencer.md).
