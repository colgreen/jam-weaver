# Generator-specific variation

## Goal

Make `vary` produce another recognizable member of the candidate's generator
family. Mutation must preserve the musical identity and structural invariants
that made the source generator useful rather than applying unrelated edits to
arbitrary steps.

The candidate's recipe lineage selects mutation behavior. The generator
currently selected for `new` does not affect variation of an existing
candidate.

## Current problem

Bare `vary` uses `PatternMutator`, whose bounded but structure-blind edits can
change rests, pitches, velocity, and gate anywhere in a pattern. On a repeated
four-bar loop it can independently edit several copies of the same bar and
destroy their audible relationship.

Targeted forms such as `vary rhythm` use `PhrasePatternMutator` for every
melodic pattern, regardless of which generator created it. That mutator
protects a structural mask but does not understand Euclidean loop identity,
motif archetypes, groove vocabulary, or a repeated A bar with a turnaround.

Mutation recipes currently replace the parent generator ID with a generic
mutation ID without retaining a dispatchable generator-family identity.
Consequently, repeated variation cannot reliably select the source family's
rules from the immediate recipe alone.

## Intended contracts

- `vary` dispatches from the candidate's recorded generator family and version,
  never from `GenerationControls.Mode`.
- Each supported generator family defines its allowed targets, edit budgets,
  protected structure, and strength semantics.
- A variation records parent ancestry, the stable family identity, mutation
  strategy version, target, strength, seed, and resolved edits needed for
  explanation and deterministic reconstruction.
- Repeated `vary` operations retain the same family identity unless an explicit
  transformation changes families.
- An unsupported family or target produces a clear error. Performer-facing
  variation never silently falls back to structure-blind mutation.
- Materialized steps remain authoritative and playable when mutation or recipe
  reconstruction support is unavailable.
- Generator-specific mutation remains in the core; the console parses commands
  and reports capabilities but does not contain musical edit rules.

## Family behavior to define

### Euclidean one-bar loop

- Rhythm variation moves a bounded number of onsets while retaining onset count,
  bar length, and intelligible metre.
- Note variation changes motif pitches through nearby scale-degree motion while
  retaining the small vocabulary.
- Expression variation changes velocity or gate without changing rhythm or
  pitch.
- Bare `vary` selects one supported dimension; it does not combine unrelated
  edits at ordinary strength.

### Repeated loop with turnaround

- The form remains `A | A | A | A-turnaround`.
- Rhythm, note, and expression edits to A are mirrored across its repetitions.
- The final-bar turnaround is preserved or recalculated after an A edit.
- Turnaround variation changes only the final approach-and-resolution gesture.
- Low-strength bare variation favors the turnaround. Higher strength may add
  one mirrored A edit without obscuring the loop identity.

The experimental turnaround generator must remain throwaway until listening
establishes that it belongs in the retained generator set. Its mutator is the
first concrete design exercise, not an implicit commitment to permanent API or
persistence support.

### Other existing families

- Structured phrase variation preserves phrase roles and structural anchors.
- Motif variation preserves its selected archetype and characteristic contour.
- Groove variation preserves the selected vocabulary family and bass-only
  contract.
- Euclidean phrase variation preserves whatever identity dimension is chosen
  for that experimental form; this requires an explicit decision before
  implementation.
- Drum variation requires separate voice-aware rules and must not inherit
  melodic targets accidentally.

## Refactor stages

1. Introduce a small typed mutation contract that accepts a materialized parent
   and generator-specific settings. Do not add runtime discovery or a generic
   strategy registry.
2. Define a stable generator-family lineage value and carry it through mutation
   recipes. Decide how existing generator and mutation recipes map to that
   lineage without changing materialized playback.
3. Move console mutation selection into an explicit core-facing dispatcher
   composed from known mutators. Keep the switch visible until a second runtime
   discovery consumer exists.
4. Implement the repeated-loop/turnaround mutator first. Verify mirrored A
   edits, isolated turnaround edits, deterministic output, and repeated
   variation dispatch.
5. Implement the one-bar Euclidean mutator and compare targeted rhythm, note,
   and expression changes by ear.
6. Adapt the existing structured phrase mutator so its family and supported
   targets are explicit rather than treating it as universal melodic mutation.
7. Add family-specific mutators only for generators retained after listening.
   Unsupported experimental generators should reject `vary` clearly.
8. Remove `PatternMutator` from normal console composition. Retain or delete its
   recipe reconstruction separately according to saved-recipe compatibility
   needs.
9. Update help so available targets are described for the candidate family;
   changing the generator selected for `new` must not change those capabilities.
10. Update current-state generation and candidate-workflow documentation after
    the resulting behavior is established.

## Verification

- Same parent, family settings, target, strength, and seed produce identical
  material and recipe metadata.
- Representative different seeds produce different valid variations.
- A mutation recipe routes a second `vary` to the same family.
- Switching `GenerationControls.Mode` before `vary` does not change dispatch.
- Unsupported families and targets fail with actionable messages.
- Each family has tests for its preserved rhythm, form, vocabulary, contour,
  register, timing, and MIDI-value invariants as applicable.
- Edit counts are bounded and strength increases them predictably without
  combining dimensions unexpectedly.
- Mutation ancestry and reconstruction reject missing, unknown, mistyped, or
  inconsistent lineage parameters.
- Saved materialized patterns remain loadable when their mutation algorithm is
  not available.
- Run `dotnet build` and the full automated test suite.
- Audition family-specific variations on the intended MIDI hardware; automated
  invariants do not establish musical usefulness.

## Decisions before implementation

- Choose the exact lineage representation: a recipe parameter on every
  mutation, a dedicated recipe field, or family-specific mutation IDs with an
  explicit mapping. Prefer the smallest option that supports repeated mutation
  and saved-recipe validation.
- Decide whether bare `vary` chooses a target deterministically from the seed or
  whether every family defines one default target.
- Decide whether old `controlled-mutation` candidates remain variably
  compatible or become playable but unsupported parents.
- Decide which current experimental generators merit a mutator before building
  one for each exposed mode.

## Completion criteria

- Performer-facing `vary` never invokes structure-blind generic mutation.
- Every supported generator family documents and tests its variation contract.
- Repeated variation preserves family identity and dispatches correctly.
- Unsupported variation is explicit and does not alter the candidate.
- Console help reflects the candidate's actual variation capabilities.

Related: [generator strategy](generator-strategy.md), [pattern generation](../generation/pattern-generation.md),
and [candidate workflow](../performance/candidate-workflow.md).
