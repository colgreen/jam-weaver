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
- Removing the clock cable at the USB interface's MIDI IN, without sending
  Stop, produces `ClockLost`. The performer confirms silence without a hanging
  note, while the output connection remains available for note cleanup.
- Reconnecting that cable restores incoming clock pulses but leaves transport
  clock-lost at its preserved position and the synth silent.
- Stop followed by Start restores audible playback after clock loss; the
  observed Start resets transport position to pulse zero.

Console status reports no player or internal-clock error during these checks.
Playback, restart, and pattern-change quality are subjective listening results;
the terminal observations do not measure the exact activation instant.

## Isolating clock loss

Keep USB and the output MIDI cable connected. With the clock source running,
remove only the incoming MIDI cable at the USB interface's MIDI IN socket.
This leaves the output available for Note Off during watchdog cleanup.

An upstream cable interruption through the tested splitter/merger arrangement
was accompanied by an incoming MIDI Stop. That exercises ordinary Stop cleanup,
not an isolated clock timeout. The origin of that Stop is not established.
Check for `ClockLost` and absence of an intervening Stop before calling the
timeout path validated.

A temporary runner recorded classified input messages and transport changes,
with trace printing outside MIDI callbacks. No Clock messages were filtered
during the successful physical interruption and recovery checks.

## Not yet established by this check

- MIDI Continue, including direct recovery from ClockLost without Stop first.
- Starting with an already pending replacement after Stop.
- Native port replacement during playback, disconnects, or driver failures.
- Quantitative timing, drift, sustained load, or long-duration reliability.
- Internal-clock shutdown messages or device-specific MIDI controls.

See [clock and transport](../midi/clock-transport.md),
[port ownership](../midi/port-ownership.md), and the separate model-specific
[Circuit](circuit-validation.md) and [Zynthian](zynthian-validation.md) evidence.
