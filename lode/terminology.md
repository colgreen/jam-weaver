# Terminology

- Active note - A Note On sent by this process for which it has not yet sent a
  corresponding Note Off.
- CC - MIDI Control Change message carrying a controller number and 7-bit value.
- Candidate - A generated or mutated pattern being privately auditioned before
  acceptance.
- Clock-lost - Transport is paused because the selected external clock exceeded
  its timeout; Start or Continue is required to recover.
- Generator version - Stable identifier for the exact deterministic algorithm
  used by a recipe.
- Groove vocabulary - A versioned set of project-authored rhythmic skeletons
  used as recognizable starting points for controlled bass-pattern variation.
- MIDI Clock - MIDI real-time timing messages sent at 24 PPQN.
- Motif archetype - A named short contour and rhythm whose controlled repetition
  and transformation form a four-bar melodic phrase.
- Musical role - A friendly register and behavior profile: bass, middle, or high.
- Pattern - Materialized step data that can be played exactly and saved with its
  generative recipe.
- Pattern library - The working-directory `patterns` folder containing one
  versioned JSON file for each saved accepted pattern.
- PPQN - Pulses per quarter note.
- Phrase - A one-, two-, or four-bar melodic pattern whose bars have related
  structural roles; the four-bar form is A, A-prime, B, and turnaround.
- Recipe - Generator name, parameters, seed, and ancestry required to reproduce
  or mutate a pattern.
- Random source - A seeded Redzen `IRandomSource` created through
  `RandomDefaults.CreateRandomSource(seed)`; generator versions protect its
  output contract.
- Scale degree - A pitch position relative to the active musical palette; melodic
  patterns store this rather than binding their shape to absolute MIDI notes.
- Track - A sequence routed to one MIDI output and channel; the initial product
  scope has one track.
