# Phrase generation

`melodic-structured-phrase` version 1 is implemented by
`StructuredPhraseGenerator` with `PhraseGeneratorSettings` and selected with
`generator phrase`. It produces one, two, or four 16-step melodic bars. Four-bar
phrases use A, A-prime, B, and turnaround roles:

- A establishes the rhythmic and melodic identity.
- A-prime moves selected non-downbeat onsets while retaining A's downbeat.
- B provides seeded contrast while retaining an identity anchor.
- The turnaround changes the ending and leads back to A or creates space.

Activity, rhythm character, movement, variation, turnaround strength, musical
role, and tonal context are explicit settings. Rhythm character can be steady,
syncopated, or broken. Role profiles limit density and pitch movement.

The generator builds rhythm first, marks structural anchors, then develops a
short pentatonic motif across the phrase. Every bar contains a downbeat note.
Role and activity bound the initial hit count; role bounds pitch range, and
movement bounds motif pitch changes. Rhythm uses weighted step selection;
there are no explicit maximum-gap or cluster constraints.

`variation` changes motif length, the number of A-prime rhythm edits, and ghost
eligibility. B is independently constructed with shared anchors even at low
variation. `turnaround none` disables final-bar rhythm edits, not melodic
development or ghost eligibility. Two-bar output uses A and an ending rhythm,
but its pitch rule is the same as four-bar A-prime; the stable final-pitch rule
applies only to bar four.

Hits are classified as structural, ordinary, pickup, ghost, or phrase ending.
Structural and ordinary hits always play. Ghosts are sparse, quieter, shorter,
and use deterministic trigger probability. Trigger decisions use prepared
musical content rather than the pattern's random snapshot ID, so equivalent
generated content varies identically during playback.

Targeted mutation can change rhythm, notes, expression, turnaround, or all four.
It protects structural anchors and keeps timing, role, tonal context, ancestry,
and at least one note per bar intact.

Recipes record the friendly inputs and derived masks needed to reconstruct and
explain the phrase. The Euclidean generator remains separately versioned;
phrase generation does not change its output contract.

The implementation and structural invariants are automatically tested. Its
musical quality still requires subjective hardware audition.
The [revisit plan](../plans/revisit-phrase-generator.md) proposes stronger
preservation of A, related B development, clearer control semantics, and
consistent endings. The current version-1 behavior remains in use until that
work is designed, implemented, and evaluated.

Related: [pattern generation](pattern-generation.md) and [candidate
workflow](../performance/candidate-workflow.md).
