# Pleasing sequence research

## Purpose and limits

This reference summarizes research relevant to generating short melodic MIDI
sequences that are useful and pleasing in a live jam. It informs experiments;
it does not establish a universal formula for musical quality. Preference
depends on listener, style, instrumentation, context, exposure, and bodily
participation. JamWeaver still requires listening on its intended hardware and
in an ensemble context.

The strongest working idea is to create a concise, metrically intelligible
rhythmic identity with a small coherent pitch vocabulary, repeat it enough to
become predictable, and introduce no more than one clearly audible source of
variation at a time.

## Rhythm: prediction with bounded surprise

Listener studies commonly find the strongest groove response at moderate
syncopation. A completely regular rhythm creates little tension, while too much
syncopation weakens the perceived metrical model. The placement and strength of
individual syncopations matter; a scalar syncopation score is not a complete
model of groove.

Engineering implications:

- Establish the metre with stable anchors before contradicting it.
- Prefer a small number of distinctive offbeats to pervasive displacement.
- Let a syncopation lead to a confirming onset or an intelligible resolution.
- Do not increase onset density and syncopation automatically as one notion of
  complexity.
- Score Euclidean rotations for metrical function, not only evenness and first
  step occupancy.

Sources:

- Sioros et al., [Syncopation creates the sensation of groove in synthesized
  music examples](https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2014.01036/full),
  2014. Controlled transformations of simple melodies found higher groove
  ratings for moderate than absent or maximal syncopation and found that the
  character of individual displacements mattered.
- Vuust and Witek, [Rhythmic complexity and predictive
  coding](https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2014.01111/full),
  2014. Interprets the observed inverted-U relation between syncopation and
  groove through preservation and violation of metrical predictions.

## Repetition and limited vocabulary

Repetition communicates musical identity at more than one structural level.
Analyses of popular music find that individual pieces use limited internal
vocabularies and non-random repetition. Research on repeated listening also
shows that familiarity can increase liking, although the effect depends on
musical and listening context.

Engineering implications:

- Exact repetition is a valid musical result, not failed development.
- Generate a good loop before deciding whether it needs a phrase form.
- Keep rhythm and pitch vocabulary small within an individual candidate.
- Make phrase variation optional and evaluate whether it preserves audible
  identity rather than merely satisfying a changed-step bound.
- Audition a candidate for enough cycles to judge it as a loop; immediate
  novelty should not dominate evaluation.

Sources:

- Dai, Yu, and Dannenberg, [What is missing in deep music generation? A study
  of repetition and structure in popular
  music](https://ismir2022program.ismir.net/poster_136.html), 2022. Finds
  hierarchical repetition, limited within-song vocabulary, and interactions
  between repetition, rhythm, melody, harmony, and predictability.
- Madison and Schiölde, [Repeated Listening Increases the Liking for Music
  Regardless of Its Complexity](https://pmc.ncbi.nlm.nih.gov/articles/PMC5374342/),
  2017. Reports increased liking across repeated naturalistic exposure and a
  strong contribution from prior familiarity with the style. This supports
  longer audition but does not imply that every repeated loop becomes useful.

## Melody: proximity, contour, and local expectation

Melodic-expectation experiments support proximity strongly: smaller next-note
intervals tend to receive higher continuation ratings. Coherent contour also
matters. Continuing direction after a small interval has experimental support;
automatic reversal after every large leap is a less reliable rule. Expectation
is shaped by both learned stylistic regularities and short-term patterns heard
inside the current melody.

Engineering implications:

- Default to repeated pitches and adjacent scale-degree movement.
- Give a leap an identifiable structural job rather than permitting it merely
  because it remains in range.
- Preserve a motif's up/down contour and characteristic transitions when
  developing it.
- Resolve endings through nearby motion as well as tonal stability.
- Penalize jagged alternation independently from maximum leap size.
- A small local transition model learned from the generated motif may be more
  useful than unconstrained pitch selection or a large corpus model.

Sources:

- McDermott et al., [Expectations for Melodic Contours Transcend
  Pitch](https://pmc.ncbi.nlm.nih.gov/articles/PMC4605576/), 2016. Reports
  strong proximity effects and evidence for directional inertia after small
  movements, while not finding the predicted preference for post-leap reversal
  in its experiment.
- Pearce and Wiggins, [Auditory Expectation: The Information Dynamics of Music
  Perception and
  Cognition](https://onlinelibrary.wiley.com/doi/10.1111/j.1756-8765.2012.01214.x),
  2012, and Pearce, [Statistical learning and probabilistic prediction in music
  cognition](https://nyaspubs.onlinelibrary.wiley.com/doi/10.1111/nyas.13654),
  2018. IDyOM models expectations using variable-length context and both
  long-term learned style and short-term musical material. JamWeaver can apply
  this principle without adopting IDyOM itself.

## Euclidean rhythm as a skeleton

Euclidean rhythms distribute onsets as evenly as possible, and many generated
patterns correspond to established musical timelines. This gives the current
baseline a plausible musical foundation. Evenness alone does not choose a
metrical rotation, accents, durations, pitch assignments, or a convincing
turnaround.

Source:

- Toussaint, [The Euclidean Algorithm Generates Traditional Musical
  Rhythms](https://archive.bridgesmathart.org/2005/bridges2005-47.html), 2005.
  Shows the connection between Euclidean onset distribution and a family of
  traditional timeline rhythms.

## Hierarchy without compulsory complexity

Contemporary symbolic generation research often separates motif generation
from phrase and section development and constrains repetition, contour, rhythm,
and form at their appropriate levels. This supports staged generation, but it
does not show that JamWeaver should always produce a developed phrase.

Source:

- Dai, Jin, Dannenberg, and Wang, [Controllable deep melody generation via
  hierarchical music structure
  representation](https://www.cs.cmu.edu/~rbd/papers/frameworks-ismir2021.pdf),
  2021. Factors longer-form generation into motif, phrase, and section stages
  governed by explicit repetition and musical-form constraints.

## Experiments suggested by the evidence

Run small, interpretable comparisons against `EuclideanMotifGenerator` version
3 rather than combining several ideas in one new engine:

1. Rank Euclidean rotations by strong-beat anchors, bounded gaps, moderate
   syncopation, a distinctive offbeat, and loop-ending suitability.
2. Generate a three- or four-note vocabulary using mostly repeats and adjacent
   scale-degree movement, at most one purposeful leap, and nearby resolution.
3. Compare the baseline with `A | A | A | A-turnaround`, changing only rhythm,
   pitch, or articulation in the second half of the last bar.
4. Compare `A | answer | A | answer-prime`, preserving exactly one declared
   identity dimension such as rhythm, contour, or pitch order.
5. Generate a strong one- or two-bar loop first and expose phrase development
   as an optional transformation: repeat, ending, answer, space, or contrast.
6. Compare a very small authored bass-rhythm vocabulary with Euclidean rhythms
   before extending the existing vocabulary search machinery.

Neural generation, large corpus models, evolutionary search, microtiming, and
a universal pleasantness score are lower priorities until symbolic loop quality
and phrase-development value are understood.

Related: [pattern generation](pattern-generation.md), [Euclidean phrase
generation](euclidean-phrase-generation.md), and [generator
strategy](../plans/generator-strategy.md).
