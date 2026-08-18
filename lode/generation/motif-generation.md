# Motif generation

`melodic-musical-motif` version 1 is the startup default. It creates a small bass
idea and develops it conservatively over four bars. Every note is deliberate:
trigger probability is always one, and there are no ghost notes or independent
per-note velocity changes.

The available shapes are pedal, root-fifth, walking, call-response, arch,
pickup, and riff. `auto` chooses one deterministically from the seed. A shape
supplies both a short contour and a rhythm; activity, movement, and variation
adjust it without discarding its identity.

The motif normally contains three to five notes, begins on the root, stays in
the selected pentatonic palette, and resolves inside the bass MIDI range.

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

Tests protect deterministic output and compositional constraints. They do not
establish musical quality; fixed seeds should be auditioned on physical hardware
before contours or defaults are tuned.

Related: [pattern generation](pattern-generation.md), [phrase
generation](phrase-generation.md), and [groove generation](groove-generation.md).
