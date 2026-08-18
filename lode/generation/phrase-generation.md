# Phrase generation

`melodic-structured-phrase` version 1 produces one, two, or four 16-step melodic
bars. Four-bar phrases use A, A-prime, B, and turnaround roles:

- A establishes the rhythmic and melodic identity.
- A-prime keeps at least half of A's rhythmic identity while varying it.
- B provides seeded contrast while retaining an identity anchor.
- The turnaround changes the ending and leads back to A or creates space.

Activity, rhythm character, movement, variation, turnaround strength, musical
role, and tonal context are explicit settings. Rhythm character can be steady,
syncopated, or broken. Role profiles limit density and pitch movement.

The generator builds rhythm first, marks structural anchors, then develops a
short pentatonic motif across the phrase. Every bar contains a note, and density,
silent gaps, clusters, pitch range, repeated pitches, and large leaps remain
bounded.

Hits are classified as structural, ordinary, pickup, ghost, or phrase ending.
Structural and ordinary hits always play. Ghosts are sparse, quieter, shorter,
and use deterministic trigger probability. Trigger decisions use prepared
musical content rather than the pattern's random snapshot ID, so equivalent
generated content varies identically during playback.

Targeted mutation can change rhythm, notes, expression, turnaround, or all four.
It protects structural anchors and keeps timing, role, tonal context, ancestry,
and at least one note per bar intact.

Recipes record the friendly inputs and derived masks needed to reconstruct and
explain the phrase. The original simple generator remains separately versioned;
phrase generation does not change its output contract.

The implementation and structural invariants are automatically tested. Its
musical quality still requires subjective hardware audition.

Related: [pattern generation](pattern-generation.md) and [candidate
workflow](../performance/candidate-workflow.md).
