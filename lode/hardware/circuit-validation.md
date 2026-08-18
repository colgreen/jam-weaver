# Original Novation Circuit validation

> Status: the initial melodic performance path is hardware-validated. Timing
> measurement and device-specific control are not validated.

The original Novation Circuit has been exercised through a USB MIDI interface
using standard MIDI 1.0 messages on channel 1. Monitoring used the performer's
headphone Aux workflow at controlled volume.

## Confirmed behavior

- Individual middle- and bass-register Note On/Off messages sound and stop.
- A fixed-seed 16-step bass pattern plays through the complete
  generator-to-player-to-device path using internal clock.
- Internal clock sounded steady in a short subjective check at 100 BPM.
- Stop and Panic leave no audible hanging note.
- Mutation, chromatic root audition, acceptance, and rejection activate
  through the bar-quantized candidate path without an observed glitch.
- An accepted fixed-seed pattern saves to JSON, survives application restart,
  loads with its name/seed/key/role intact, and sounds the same on playback.
- An Arturia KeyStep acting as external clock master supplies Start, 24-PPQN
  Clock, and Stop through the interface input; the application drives Circuit
  notes while preserving stopped position.
- Removing external clock without Stop enters ClockLost, silences playback after
  the configured timeout, leaves no hanging note, and does not fall back to
  internal clock.

External clock is consumed locally and is not relayed to the Circuit. This avoids
creating a feedback loop in the tested two-way cabling arrangement.

## Not yet established

- Quantitative internal-clock jitter or long-duration drift.
- Behavior under substantial CPU load or very long sessions.
- Hardware behavior for Continue after Stop or recovery after ClockLost, though
  both state contracts have automated tests.
- Device disconnect/driver failure while a note is active.
- Circuit CC assignments, program selection, effects automation, SysEx, or
  patch management.
- Drum-pattern playback through a configured Circuit drum track.

Related: [clock and transport](../midi/clock-transport.md),
[candidate workflow](../performance/candidate-workflow.md), and
[pattern library](../persistence/pattern-library.md). Generic second-device
evidence is recorded in [Zynthian validation](zynthian-validation.md).
