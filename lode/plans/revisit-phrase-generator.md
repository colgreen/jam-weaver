# Revisit the phrase generator

## Goal and scope

Develop `phrase` into a useful choice for controllable one-, two-, and four-bar
melodic phrases. Retain its explicit length, rhythm character, activity,
movement, role, and tonal context while making related bars audibly retain the
opening idea. Musical usefulness must be established through listening.

The current `StructuredPhraseGenerator` remains version 1 and selectable while
this plan is pending. This is a plan for subsequent work, not an implemented
version 2. `euclidean-phrase` and its `euclidean2` alias are retired from console
selection; their core algorithm, reconstruction, and saved-pattern variation
remain supported for compatibility.

The Euclidean one-bar generator is the frozen musical comparison baseline.
Turnaround Loop, motif, and groove retain their own purposes and algorithms.
This work does not require a shared strategy framework, additional generators,
new palettes, or changes to MIDI playback.

## Current foundation and limitations

`StructuredPhraseGenerator` builds a rhythm, a shared short melody, and related
bars with accents and optional ghosts. Its length and steady/syncopated/broken
controls offer a distinct direction beyond the other generators.

Current implementation details to address:

- A uses weighted selection of individual steps. It has a downbeat and a target
  hit count but no explicit rest-gap or cluster constraints.
- A-prime and ending rhythm edits can move a hit anywhere among non-downbeat
  steps, weakening rhythmic identity even with a small edit count.
- B constructs a new rhythm with shared downbeat and possible middle-bar
  anchor. Low variation does not prevent this independent reconstruction.
- Pitches follow onset ordinal rather than a retained source event. Moving a
  hit can reassign pitches at other positions and change an important accent.
- Variation changes base motif length (3, 4, or 6 notes), A-prime edit count,
  and ghost eligibility together. Development controls can rewrite A itself.
- Two-bar output labels its final bar as an ending but uses the A-prime pitch
  rule. Only bar four uses stable final-pitch resolution, and that selection
  compares octave offsets rather than actual pitch distance.
- `turnaround none` disables ending rhythm edits but retains melodic ending
  behavior and possible ghosts.
- `Vary` delegates to shared mutators rather than preserving phrase-specific
  relationships through repeated edits.

These are code observations and musical hypotheses, not established causes of
poor listening results. See [current phrase behavior](../generation/phrase-generation.md).

## Proposed musical contract

- One bar establishes A; two bars provide A and a related ending; four bars
  establish A, vary it, provide a related answer B, and close the phrase.
- Base A remains identical for a given seed and base controls when only length,
  development strength, or ending strength changes. Define a stable separation
  of base and development random choices before implementing this promise.
- Development preserves named anchors and a recognizable rhythmic or melodic
  identity. A moved note carries its pitch and expression unless the selected
  transformation explicitly changes them.
- Low variation preserves most of A; medium adds a related answer; high allows
  greater contrast within explicit edit budgets. Define what each level
  protects and changes, including which dimensions may change together.
- Endings use the same musical rules for the final bar at either length.
  Nearby resolution is measured in resolved pitch distance. Decide whether
  `none` means exact repetition or just absence of a closing gesture.
- Ornamentation has an explicit, predictable policy and cannot silently change
  the structural identity when development strength changes.

## Staged experiments

1. **Establish listening fixtures.** Use a fixed set of seeds at matched role,
   root, palette, activity, tempo, and sound. Begin with bass, then middle and
   high. Compare phrase v1 with the Euclidean baseline and Turnaround Loop;
   repeat each candidate enough times to judge loop identity. Record keep/reject
   preference and specific rhythm, melody, space, and ending observations.
2. **Improve and evaluate A alone.** Isolate one-bar results. Assess repetition,
   melodic motion, accent, rest gaps, and clusters. Try one constraint at a
   time; retain weighted rhythm construction initially so changes have an
   attributable effect. Avoid tuning development around a weak source bar.
3. **Run a minimal development experiment.** Freeze A. Replace unrestricted
   onset moves with bounded nearby moves, initially within the same half-bar,
   and carry each moved event's pitch and expression. Derive B from A instead
   of rebuilding it. Start with one rhythmic change while pitch stays fixed,
   then compare pitch-only development with fixed rhythm. Confirm exact edit
   distances and budgets with the human before implementation.
4. **Clarify controls and endings.** Separate source complexity from
   development strength. Decide how ornamentation is controlled without adding
   unnecessary knobs. Apply final-bar rules consistently at two and four bars
   and make `turnaround none|subtle|strong` describe its audible effect. Compare
   this with the minimal experiment before adding more transformations.
5. **Define phrase-owned variation.** Align `vary` targets and strengths with
   the retained development rules. Preserve phrase roles and anchors across
   repeated variation from materialized content. Coordinate with the
   [generator-specific variation plan](generator-specific-variation.md).
6. **Retain only useful changes.** Compare against v1 and the loop baselines
   across the fixed seed set. Decide whether improvement warrants a version 2,
   which defaults to use, and whether the engine merits its console place.
   Update help and current behavior documentation to match the chosen contract.

## Versioning and verification

Any changed musical output requires a new generator version. Keep version-1
generation/reconstruction available through an explicit version-aware path;
saved materialized content remains authoritative. Define variation dispatch for
both version-1 and version-2 lineage before exposing a new version. Do not
silently reinterpret old recipes or change the frozen Euclidean baseline.

Automated verification must cover deterministic output and recipe
reconstruction, saved-pattern round trips, role/palette ranges, all lengths,
base-A stability across development controls, protected anchors, bounded event
moves, pitch/expression retention, and ending behavior at both phrase lengths.
Test repeated `vary` and compatibility with version-1 material. Add fixed-seed
snapshots only once the new version's rules are settled.

Run `dotnet build` and both xUnit v3 executables. Hardware listening is a
separate acceptance step; structural metrics cannot establish musical quality.
Retain fixture settings and coarse observations so a later change can be
compared against the same examples.

## Decisions before implementation

- Which rhythmic and melodic anchors define A's identity?
- What local move distance and edit budget should each variation level allow?
- How much contrast should B provide at low and medium variation?
- What should no turnaround promise, and should ghosts be optional separately?
- What listening improvement is sufficient to retain a proposed change?

Completion requires clearer, repeatable controls; recognizable relationships
between bars; consistent endings; preserved compatibility; and human listening
evidence supporting the retained changes.

Related: [generator strategy](generator-strategy.md), [pattern generation](../generation/pattern-generation.md),
[phrase generation](../generation/phrase-generation.md), and
[pleasing sequence research](../generation/pleasing-sequence-research.md).
