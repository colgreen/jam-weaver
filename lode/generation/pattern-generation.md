# Pattern generation

> Implementation status: deterministic simple, structured phrase, and bass
> groove generation, drum generation, mutation, recipes, playback, and candidate
> controls are implemented and tested.

Generation must produce intentional, repeatable musical structure rather than
independent random notes. Every generator is deterministic for its generator
version, parameters, seed, and input pattern.

Randomness comes from the pinned Redzen package via
`RandomDefaults.CreateRandomSource(seed)`. A Redzen upgrade or change to its
default random source is output-affecting and requires generator-version review.

## Layered melodic generation

The initial melodic generator works in layers:

1. Build a controlled rhythm, initially using Euclidean distribution.
2. Construct a short motif from a pentatonic palette.
3. Repeat the motif with bounded variation.
4. Constrain pitches to the selected musical role.
5. Apply restrained velocity and gate variation.

The performer controls friendly musical dimensions rather than raw probability
tables:

- Density: sparse through busy.
- Movement: repeated notes through wider pitch movement.
- Repetition: stable motif through greater variation.
- Role: bass, middle, or high.
- Gate: short/plucky through sustained.
- Mutation strength: subtle through substantial.

## Structured phrase generation

The structured phrase generator is `melodic-structured-phrase` version 1. It emits
one, two, or four 16-step bars; four bars follow A/A'/B/turnaround form. A'
retains at least half of A's rhythmic identity, B provides seeded contrast, and
the turnaround varies the ending while leading back to the downbeat.

Friendly categorical controls select length, activity, steady/syncopated/broken
rhythm, movement, variation, and turnaround strength. Structural hits always
play. Sparse ghost hits use deterministic probability, lighter velocity, and
shorter gates. Trigger decisions use a prepared material-content key rather than
snapshot identity, so equivalent generated content has equivalent loop-to-loop
variation.

The original `melodic-euclidean-motif` version 1 remains available as `simple`
mode and its fixed-seed contract is unchanged.

## Groove vocabulary generation

The opt-in `melodic-groove-vocabulary` version 1 generator emits four bass bars
from twelve project-authored 16-step templates in foundation, offbeat,
anticipation, long-short, sparse-answer, and broken categories. Required anchors
never move. A bounded 64-candidate search creates A', B, and turnaround masks and
ranks them using density, project syncopation, circular gaps/clusters, Hamming
distance, and exact circular onset-movement cost. Fixed-order relaxation is
recorded when no candidate meets the requested distance band.

`groove` category and `similarity close|related|contrast` are friendly controls;
activity, movement, variation, turnaround, tonal context, and seed are shared
with phrase mode. Pitch identity follows moved rhythmic events and template
accent plus phrase intention jointly determine velocity, gate, and sparse ghost
behavior. Recipe parameters retain vocabulary and metric versions, all four
masks and feature vectors, relaxations, and structural/ghost masks.

Groove mode is deliberately bass-only and remains opt-in until comparative
hardware audition supports changing the default. `compare [seed]` prepares
matched four-bar phrase and groove candidates; subsequent `compare` commands
toggle them using the normal next-bar candidate scheduling.

## Musical motif grammar

The startup default is the bass-only `melodic-musical-motif` version 1 generator.
It chooses a named pedal, root-fifth, walking, call-response, arch, pickup, or riff
archetype and develops its small contour through A/A'/B/return form. Low variation
repeats A exactly; medium changes one ending and creates a related B; high permits
one additional B change. The ending returns to a nearby stable degree.

Archetypes provide explicit sparse/medium/busy rhythms. All triggers always play,
and expression follows anchors and phrase roles without per-note randomness.
Recipes record requested/resolved shape, four masks, motif length, controls, and
classification masks. Musical quality remains a hardware-audition question.

## Musical roles

Role ranges are defaults and remain configurable:

| Role | MIDI range | Generation character |
| --- | ---: | --- |
| Bass | 36-52 | Sparse; strong repetition; mostly tonic, fifth, and octave |
| Middle | 48-72 | Moderate density; wider motifs and movement |
| High | 67-88 | More space; cautious wider intervals; lower velocity |

Changing role creates or transforms a candidate. It never unexpectedly moves an
accepted pattern that is currently being performed.

Major and minor pentatonic palettes are the initial safe choices. A performer
selects them by ear through auditioning rather than needing music-theory
knowledge. Full scales and additional modes are later extensions.

## Mutation

Mutation starts from a materialized parent and creates a new candidate. Initial
operations may:

- Change a bounded number of pitches within the current palette and role.
- Add, remove, or move triggers while respecting density bounds.
- Alter velocity or gate within conservative limits.
- Rotate a pattern or repeat/alter a motif fragment.
- Preserve steps explicitly locked by the performer when locking is introduced.

Mutation never destroys the last accepted pattern. Its seed, parameters, and
parent identifier form part of the resulting recipe.

Drum generation uses literal MIDI note mappings and may distribute multiple
voices across a pattern. Device-specific drum maps are outside the first generic
generator and can later be supplied by optional profiles.

Structured melodic mutation can target rhythm, notes, expression, turnaround,
or all dimensions. Phrase structural masks protect anchors; legacy or manually
transformed patterns conservatively protect sounding bar downbeats. Candidate
history retains up to eight audition snapshots independently of accepted undo.

Related: [pattern model](../sequencer/pattern-model.md) and
[candidate workflow](../performance/candidate-workflow.md).
