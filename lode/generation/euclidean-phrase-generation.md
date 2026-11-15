# Euclidean phrase generation

`melodic-euclidean-2` version 1 is implemented by `EuclideanPhraseGenerator`
with `EuclideanGeneratorSettings`. It is a retired four-bar development
experiment. Console selection rejects both `generator euclidean-phrase` and
its former `generator euclidean2` alias. It overlaps with other phrase options
and has not demonstrated the one-bar baseline's musical usefulness.

The unchanged core algorithm and recipe reconstruction remain available for
compatibility. Saved patterns still load and play their authoritative
materialized steps, and console `vary` still resolves this generator's lineage
for saved candidates, including repeated variations. There is no console mode
for creating new Euclidean phrase material.
The existing `euclidean` generator remains a separate, frozen one-bar baseline;
Euclidean phrase experiments must not change its implementation or recipe contract.

Euclidean phrase keeps the baseline's pentatonic pitch model, role ranges, Euclidean
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

New phrase development work follows the [structured phrase
plan](../plans/revisit-phrase-generator.md). Retaining this compatibility path
does not imply further tuning of the retired experiment.

Related: [pattern generation](pattern-generation.md) and [candidate
workflow](../performance/candidate-workflow.md).
