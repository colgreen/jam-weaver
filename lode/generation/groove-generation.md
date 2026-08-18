# Groove generation

`melodic-groove-vocabulary` version 1 tests whether a curated rhythm vocabulary
sounds less mechanical than weighted step selection. It is a four-bar,
bass-only alternative and will not replace the default without comparative
hardware audition.

## Rhythm vocabulary

The version-1 vocabulary contains twelve project-authored 16-step skeletons in
six categories: foundation, offbeat, anticipation, long-short, sparse-answer,
and broken. Each template has a stable ID, required and optional notes, movable
notes, accent levels, and entry and turnaround suitability.

Template IDs and masks are recipe contracts. Changing them requires a vocabulary
version increment. Required anchors, density, silent-gap, cluster, and accent
rules are validated when the vocabulary is created.

## Variation

Each derived bar is selected from a bounded set of 64 candidates. Candidates may
move, add, or remove only permitted notes and may never remove required anchors.
They are ranked by density, syncopation, gap and cluster shape, changed steps,
onset movement, activity balance, and the requested relationship to the source.

The friendly similarity choices are close, related, and contrast. If no
candidate meets every target, secondary constraints relax in a fixed recorded
order; anchors and safety bounds never relax.

Four bars have distinct jobs: A establishes the template, A-prime stays close,
B provides the requested contrast, and the turnaround concentrates change in
the second half of the final bar.

Pitch and articulation move with a rhythmic event rather than being reassigned
by its new position in the note list. Velocity and gate follow template accent,
surrounding space, and phrase role. Sparse ghost behavior is deterministic;
there is no independent velocity randomness.

Recipes record vocabulary and metric versions, template ID, settings, all four
rhythm and accent masks, measured features, any relaxed constraint, and motif
transformations. Materialized steps remain authoritative.

Automated metrics constrain and explain output; they are not evidence of musical
quality. Comparison should use matched seeds and settings on physical hardware
and record coarse preference across several examples.

Related: [pattern generation](pattern-generation.md) and [phrase
generation](phrase-generation.md).
