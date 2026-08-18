# Initial sequencer plan

> Status: Stages 1-10 are implemented or validated. The groove and musical-motif
> generators await comparative hardware audition.

The first sequencer is a single-track, MIDI 1.0 generative instrument for live
jams. It targets generic devices; the original Novation Circuit is the first
hardware test device, followed by Nord Drum and Zynthian as useful validation
targets.

## Included

- One output route and MIDI channel with multi-note steps.
- A 16-step, sixteenth-note 4/4 default pattern.
- Deterministic melodic generation and controlled mutation.
- Bass, middle, and high musical roles.
- Major/minor pentatonic audition and semitone transposition by ear.
- Candidate, accept, reject/undo, save, and recall workflow.
- Versioned JSON containing materialized patterns and generator recipes.
- Bar-quantized changes.
- External clock with simple stop-on-loss behavior.
- Switchable internal clock.
- Terminal UI separated from the engine.

## Excluded initially

- Manual MIDI recording.
- Multiple simultaneous tracks.
- Chord progressions or automatic room-audio key detection.
- Browser or desktop graphical UI.
- Automatic CC, effects, patch, or program automation.
- Audio mixing or mixer-fade automation.
- Device-specific SysEx and patch management.

## Implementation stages

1. **Complete:** Extract MIDI output and transport from the console prototype
   behind testable interfaces while preserving note-safety behavior.
2. **Complete:** Implement and test the pattern, step, pitch, recipe,
   and routing domain model. See the [detailed Stage 2 plan](stage-2-domain-model.md).
3. **Complete:** Implement deterministic generation, musical roles,
   and mutation with fixed-seed regression tests. See the
   [detailed Stage 3 plan](stage-3-generation.md).
4. **Complete:** Implement internal/external transport state and
   bar-boundary scheduling. See the [detailed Stage 4 plan](stage-4-transport.md).
5. **Complete:** Implement candidate playback, acceptance, undo, and key-finding
   controls. See the [detailed Stage 5 plan](stage-5-performance.md).
6. **Complete:** Implement versioned JSON save/recall and compatibility tests.
   See the [detailed Stage 6 plan](stage-6-persistence.md).
7. **Complete:** Exercise the complete path on the original Circuit, then
   validate generic routing against another available device. See the
   [detailed Stage 7 plan](stage-7-hardware-validation.md).
8. **Implemented:** Add four-bar structured melodic phrases, targeted mutation,
   candidate browsing, and friendly complexity controls. See the
   [detailed Stage 8 plan](stage-8-phrase-generation.md).
9. **Implemented; audition pending:** Compare Stage 8 with a curated bass rhythm vocabulary,
   perceptual variation metrics, rhythm-aware motifs, and coordinated expression.
   See the [detailed Stage 9 plan](stage-9-groove-vocabulary.md).
10. **Implemented; audition pending:** Generate bass phrases from small named
    motif archetypes with conservative A/A'/B/return development. See the
    [detailed Stage 10 plan](stage-10-musical-motifs.md).

Each stage requires review of its detailed behavior before implementation, in
accordance with [`AGENTS.md`](../../AGENTS.md).

Related: [pattern model](../sequencer/pattern-model.md),
[generation](../generation/pattern-generation.md),
[performance workflow](../performance/candidate-workflow.md), and
[clock/transport](../midi/clock-transport.md).
