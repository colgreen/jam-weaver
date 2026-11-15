# External-clock smoke coverage

The current console discovery, input ownership, transport, and playback path
has short physical smoke coverage through a USB MIDI interface and a connected
synth on channel 1. The synth model was not identified for this check; these
results establish a generic melodic path, not model-specific behavior.

## Confirmed behavior

- Native discovery lists ports, and input/output selection succeeds.
- External MIDI Start and Clock advance transport and produce audible notes
  from a fixed-seed Euclidean bass pattern in A minor pentatonic.
- External Stop leaves transport stopped at its preserved position; the
  performer confirms silence without a hanging note.
- A subsequent external Start resumes audible playback cleanly.
- A replacement generated during playback moves from pending to current while
  transport keeps advancing; the performer reports a clean musical change.
- Quitting while external clock is running exits successfully, and the
  performer confirms silence without a hanging note.

Console status reports no player or internal-clock error during these checks.
Playback, restart, and pattern-change quality are subjective listening results;
the terminal observations do not measure the exact activation instant.

## Not yet established by this check

- MIDI Continue or recovery after external clock disappears without Stop.
- Starting with an already pending replacement after Stop.
- Native port replacement during playback, disconnects, or driver failures.
- Quantitative timing, drift, sustained load, or long-duration reliability.
- Internal-clock shutdown messages or device-specific MIDI controls.

See [clock and transport](../midi/clock-transport.md),
[port ownership](../midi/port-ownership.md), and the separate model-specific
[Circuit](circuit-validation.md) and [Zynthian](zynthian-validation.md) evidence.
