# Generator strategy

## Goal

Find a small set of generators that reliably contribute musically useful
material to a live jam. Preserve the successful qualities of the one-bar
Euclidean motif baseline while treating the other current generators as
experiments that may be revised, replaced, hidden, or removed from the normal
workflow.

This plan does not assume that every existing generator deserves a permanent
place in the product. Saved materialized patterns must remain playable even if
their generator is no longer offered for new generation.

## Current evidence

- `EuclideanMotifGenerator` has produced useful results in practice. Its concise
  one-bar form, even but rotated onset distribution, and small pentatonic motif
  are the current musical baseline.
- `EuclideanPhraseGenerator` has not yet demonstrated the same usefulness. Its
  sequences can feel too complex or lack natural rhythm or melody despite its
  deliberately bounded A, A-prime, B, return transformations.
- Motif, structured-phrase, groove-vocabulary, and drum generation remain
  experiments. Automated invariants establish safety and determinism, not
  musical value.
- The current names do not form one clean taxonomy. `motif` and `phrase`
  describe musical structure, `euclidean` describes a rhythm algorithm,
  `groove vocabulary` describes a source and variation strategy, and `drum`
  describes output material.

## Working classification

Describe an engine by independent capabilities rather than placing it in a
single musical class:

- Output domain: melodic or drums.
- Scope: repeating loop or developed phrase.
- Rhythm source: Euclidean distribution, archetype grammar, constructed
  rhythm, or curated vocabulary.
- Pitch source: short motif, contour grammar, event-attached line, or fixed drum
  notes.
- Form: repeating, fixed A/A-prime/B/return, or configurable.
- Supported roles: bass, middle, high, or drums.
- Variation model: bounded transform, structural development, or metric search.
- Playback behavior: deliberate triggers only or probabilistic ornamentation.

`GeneratorMode` is therefore a selection of concrete generation engines, not a
taxonomy of mutually exclusive musical concepts. A future user interface may
present friendly engine names and capabilities without exposing this full
classification.

## Musical evaluation

Evaluate generators by repeated listening at matched tonal context, role, and
rough activity. Seeds need not yield identical structures across engines, but
comparison sets should be large enough that one unusually good or bad seed does
not decide the result. Let each candidate repeat for enough cycles to judge its
identity and usefulness as a loop rather than rating only its first impression.
Compare one changed musical dimension at a time where possible so a preference
can be attributed to rhythm, pitch, articulation, or form.

Record coarse observations rather than pretending that structural metrics
measure musical quality:

- Does the result establish a memorable identity quickly?
- Does its rhythm feel natural and support the pulse?
- Is melodic motion coherent and singable rather than merely range-safe?
- Does repetition make it usable in a jam?
- Does variation add direction without obscuring the identity?
- Does a multi-bar form justify its additional complexity?
- Are the controls musically predictable?
- How often would the performer keep the result rather than immediately
  generate another?

The one-bar Euclidean motif is the comparison baseline, not a specification
that every future generator must use Euclidean rhythm.

Research on groove, melodic expectation, repetition, and hierarchical music
generation supports a loop-first working hypothesis: establish a predictable
metrical and melodic identity, then add sparse, legible surprise. Moderate
syncopation, limited internal vocabulary, mostly nearby pitch motion, and
audible preservation of identity are promising constraints rather than proven
pleasantness rules. See [pleasing sequence
research](../generation/pleasing-sequence-research.md).

## Direction of investigation

1. Audition a representative fixed-seed set from each exposed melodic engine,
   beginning with bass and then checking middle and high where supported. Give
   short loops several repetitions before rating them.
2. Identify which qualities of the Euclidean baseline account for its practical
   usefulness by changing one dimension at a time: rotation and accent, onset
   pattern, pitch vocabulary, contour, gate, or loop length.
3. Write down recurring musical failure modes, especially unnatural accents,
   arbitrary melodic jumps, excessive event density, loss of identity between
   bars, and turnarounds that do not lead naturally back to the loop.
4. Test metrical scoring of Euclidean rotations while retaining the version-3
   generator as the frozen comparison. Prefer stable anchors, moderate
   syncopation, bounded gaps, and an intelligible return to the loop.
5. Test a small pitch vocabulary dominated by repeats and adjacent scale-degree
   motion, with purposeful leaps, coherent contour, and nearby endings.
6. For phrase experiments, change only one identity dimension at a time. Start
   with `A | A | A | A-turnaround`, limiting change to the second half of the
   final bar, then test `A | answer | A | answer-prime` while explicitly
   preserving rhythm, contour, or pitch order.
7. Compare fixed four-bar development with a simpler hierarchy in which a good
   one-bar or two-bar loop remains primary and phrase-level variation is an
   optional repeat, ending, answer, space, or contrast transformation.
8. Decide which current engines merit further tuning. Hide or remove weak
   engines from the normal console selection rather than maintaining variety
   for its own sake.
9. Try new rhythm or melody sources only when each experiment has a clear
   musical hypothesis and a listening comparison against the baseline.
10. Once the retained engines are known, simplify names, console controls,
   reconstruction wiring, tests, and documentation around that smaller set.
11. Introduce composable rhythm, motif, and phrase-development strategies only
   when a concrete experiment needs the same component in a second engine.

## Compatibility and removal

- Removing an engine from the console does not require deleting its code in the
  same change.
- Materialized steps in saved patterns remain authoritative and playable.
- Recipe reconstruction for an old generator may remain available even when
  that generator is hidden, provided its maintenance cost is reasonable.
- Deleting reconstruction support is a separate compatibility decision and
  must not make saved material unplayable.
- Generator IDs and versions remain stable for retained implementations; a
  musically significant algorithm change receives a new version.

## Decisions still needed

- Is the main performance unit a strong repeating loop, with development as an
  optional operation, or should generation usually produce a complete phrase?
- Which qualities of `EuclideanMotifGenerator` account for its usefulness: even
  onset spacing, short length, limited pitch vocabulary, repetition, its
  default density, or their combination?
- Should phrase development preserve the exact rhythm, exact pitch order, or a
  more abstract identity?
- Should weak experimental engines stay accessible behind an explicit
  experimental command, or disappear from the console once their evaluation is
  complete?
- What is the smallest useful audition record: informal notes, keep/reject
  counts, or a small named fixture set with observations?

## Completion criteria

- The exposed generator list is intentionally small and each entry has a clear
  musical purpose.
- Each retained generator has hardware-listening evidence in its intended role.
- The distinction between loop generation and phrase development is explicit.
- Controls describe predictable musical effects and unsupported controls are
  not presented for an engine.
- Current generation documentation and the project summary reflect the retained
  engines and their actual status.

Related: [pattern generation](../generation/pattern-generation.md), [Euclidean
phrase generation](../generation/euclidean-phrase-generation.md), [motif
generation](../generation/motif-generation.md), [pleasing sequence
research](../generation/pleasing-sequence-research.md), and [candidate
workflow](../performance/candidate-workflow.md).
