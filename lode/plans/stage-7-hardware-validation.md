# Stage 7: hardware validation and hardening

> Status: complete for the initial sequencer. Circuit and Zynthian melodic paths
> are confirmed; remaining limitations are explicitly recorded.

Stage 7 validates the completed instrument against physical MIDI devices. It
does not broaden the product into device-specific patch control.

## Validation sequence

1. Confirm port/channel routing with short notes and immediate cleanup.
2. Exercise fixed-seed playback with internal clock.
3. Exercise bar-quantized candidate transitions and note safety.
4. Save, restart, recall, and replay a known pattern.
5. Drive playback from external Start/Clock/Stop.
6. Remove external clock without Stop and verify ClockLost cleanup.
7. Check generic routing on a second device.
8. Record unmeasured timing and device-specific limitations.

Steps 1-6 are confirmed on the original Circuit. Zynthian completes step 7 with
raw channel-1 notes and multi-bar generated melodic playback under internal
clock. Nord Drum routing could validate raw notes, but a friendly
drum-generation command and note-map selection remain a separate design
decision.

## Completion criteria

- At least two physical devices receive the intended generic MIDI messages.
- Internal and external clock paths produce usable playback.
- Candidate changes remain musically aligned in live observation.
- Stop, mute, panic, clock loss, and shutdown produce no hanging notes.
- Save/restart/recall retains the audible pattern.
- Quantitative and device-specific claims not established by the checks are
  explicitly documented.

Related: [Circuit validation](../hardware/circuit-validation.md),
[Zynthian validation](../hardware/zynthian-validation.md), and the
[initial sequencer plan](initial-sequencer.md).
