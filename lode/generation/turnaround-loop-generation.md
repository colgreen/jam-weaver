# Turnaround Loop generation

Turnaround Loop is the performer-facing `turnaround` generator. It uses
`TurnaroundLoopGenerator` with generator ID `melodic-euclidean-turnaround`
version 1.

## Form

Generation creates a 64-step melodic pattern in the form `A | A | A |
A-turnaround`. A is a 16-step pattern produced by the version-3 Euclidean motif
baseline. The first three bars are exact copies. The fourth preserves A's
rhythm, velocity, gate, and pitches except for its final two late onsets, which
form a nearby approach and stable scale-degree resolution.

The generator supports bass, middle, and high roles and the shared major and
minor pentatonic tonal contexts. It requires sixteenth-note timing and a
16-step source bar. Generated recipes record the source generator and version,
form, and changed turnaround steps. Reconstruction is not currently supported;
materialized steps remain authoritative.

## Variation

Variation ID `melodic-euclidean-turnaround-variation` version 1 preserves the
same form:

- Rhythm moves one or two A onsets within their original half-bar, mirrors the
  move across all repetitions, and recalculates the turnaround.
- Notes moves one or two A pitches to adjacent valid scale degrees, mirrors the
  edits, and recalculates the turnaround.
- Expression changes velocity and gate for one or two A onsets and mirrors the
  changes without altering rhythm or pitch.
- Turnaround changes only the final approach-and-resolution pitches. The
  gesture vocabulary includes different approaches to the same resolution as
  well as different stable resolutions.
- `all` deterministically selects one dimension rather than combining unrelated
  edits.

Strength below 0.67 makes one A edit; strength at or above 0.67 makes two. A
bare low-strength variation favors turnaround three times out of four and
otherwise makes one mirrored note edit. Bare high-strength variation selects
one of the four dimensions from the seed.

Variation validates lineage, four-bar sixteenth-note timing, exact first-three
bar repetition, shared four-bar rhythm, and pitch-only final turnaround changes.
It rejects incompatible material rather than falling back to a generic mutator.
Recipes record parent ancestry, lineage, resolved target, strength, edit count,
and edited source-bar steps.

Automated invariants and runtime smoke checks establish deterministic structure
and safe values. Broader listening on the intended MIDI hardware is still
required to tune its gesture vocabulary and target weighting.

Related: [pattern generation](pattern-generation.md), [generator-specific
variation](../plans/generator-specific-variation.md), and [candidate
workflow](../performance/candidate-workflow.md).
