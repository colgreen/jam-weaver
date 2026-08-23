# Euclidean2 generation

`melodic-euclidean-2` version 1 is an experimental four-bar development of the
established Euclidean generator. It is selected with `generator euclidean2`.
The existing `euclidean` generator remains a separate, frozen one-bar baseline;
Euclidean2 experiments must not change its implementation or recipe contract.

Euclidean2 keeps the baseline's pentatonic pitch model, role ranges, Euclidean
onsets, velocity, gate, and deterministic seed behavior. It adds a fixed phrase
form:

- A uses the seed-selected Euclidean rhythm and short motif.
- A-prime moves at most one late onset and rotates the motif.
- B changes the onset count by at most one and reverses the motif.
- Return restores A's rhythm and motif.

The output is always four bars of sixteenth-note steps. There are no chromatic
notes, ghost notes, probabilistic triggers, or additional performer controls.
These constraints make listening comparisons with `euclidean` easier and keep
the first experiment focused on phrase development.

The recipe records each bar's onset mask and role in the phrase form. Recipe
reconstruction validates those diagnostics but regenerates deterministically
from the original controls and seed. Fixed-seed and invariant tests protect
determinism, role range safety, pitch movement, bounded rhythmic development,
and the A return.

Whether this experiment improves live musical usefulness remains a hardware
audition question. Successful ideas can be adopted in a later Euclidean2
version without altering the Euclidean baseline.

Related: [pattern generation](pattern-generation.md) and [candidate
workflow](../performance/candidate-workflow.md).
