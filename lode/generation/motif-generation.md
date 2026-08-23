# Motif generation

`melodic-musical-motif` version 4 is an experimental generator. It creates a small
melodic idea and develops it conservatively over four bars. Bass is the startup
role, while middle and high roles apply the same grammar in their own register.
Every note is deliberate:
trigger probability is always one, and there are no ghost notes or independent
per-note velocity changes.

The available shapes are pedal, root-fifth, walking, call-response, arch,
pickup, and riff. `auto` chooses one deterministically from the seed. A shape
supplies both a short contour and a rhythm; activity, movement, and variation
adjust it without discarding its identity.

Each shape/activity combination has four deterministic rhythmic variants. The
seed selects a base, pushed, pulled, or internally displaced onset pattern, so
repeated `new` commands can differ audibly without adding another console
control. A-prime makes a bounded ending change, B either rotates or displaces an
interior onset according to shape, and high variation permits a further change.
The selected variant and all four bar masks are stored in the recipe. Version 4
reflects a contour when its original direction would collapse pitches against a
role boundary; its rhythmic grammar is unchanged from version 2.

The console exposes short musical descriptions through `shape help` and
`help shape`: pedal repeats a central anchor; root-fifth alternates stable
tones; walking moves stepwise; call-response pairs a call with an answer; arch
rises and returns; pickup leads into the next bar or loop; and riff uses a
compact syncopated figure.

The motif normally contains three to five notes, begins on the root, stays in
the selected pentatonic palette, and resolves inside the selected role's MIDI
range. Changing role preserves key and scale-degree shape while fitting pitches
to the new register; subsequent generation inherits that role.

Four bars use A, A-prime, B, and return:

- Low variation repeats A exactly as A-prime.
- Medium variation changes one ending and derives a related B.
- High variation permits one additional change in B.
- The final bar returns to a nearby stable root- or fifth-class ending and may
  expose a pickup into the next loop.

Velocity and gate express anchors, motif restarts, B emphasis, and the ending.
Recipes record requested and resolved shape, controls, tonal context, four onset
masks, motif length, development choice, and classification masks. Recipe
reconstruction reproduces the material without changing the JSON schema.

Tests protect deterministic output, role ranges, and compositional constraints.
They do not establish musical quality; middle/high motifs and fixed seeds should
be auditioned on physical hardware before contours or defaults are tuned.

Related: [pattern generation](pattern-generation.md), [phrase
generation](phrase-generation.md), and [groove generation](groove-generation.md).
