# MIDI clock and transport

External MIDI Clock is the normal live source. Internal clock is a switchable
fallback for home use and testing. Both drive the same sequencer timing contract.

MIDI Clock runs at 24 PPQN. In 4/4, one sixteenth-note step is six clock pulses
and one bar is 96 pulses. Bar-quantized candidate changes use this locally counted
position.

## External source

- MIDI Start resets position to the start and enables advancement.
- MIDI Continue resumes from the preserved position.
- MIDI Stop pauses and preserves position.
- Clock pulses received while stopped are ignored for sequencer advancement.
- If clock pulses disappear beyond the configured timeout, playback stops and
  preserves position. It does not switch automatically to internal clock.
- A new Start or Continue is required after clock loss.

MIDI Clock does not identify bars on its own. Requiring Start establishes the
origin from which the application counts beats and bars.

External clock is not automatically relayed back to the device that sent it.
The console forwards incoming real-time messages only to the local transport
engine. Routing clock to other destinations is an explicit later
configuration to avoid feedback loops.

## Internal source

Internal mode sends MIDI Start, Continue, Stop, and 24-PPQN Clock to the selected
output. Tempo is adjustable. Desktop .NET is not hard real-time; timing jitter
must be measured before claiming suitability for timing-critical performance.

Clock callbacks and scheduling paths must not perform blocking UI or persistence
work. Pattern changes are prepared off the timing path and swapped atomically at
the next bar boundary.

## Runtime state

`TransportEngine` is the single source of truth for selected clock source,
Stopped/Running/ClockLost state, and pulse position. Source changes stop while
preserving position and require an explicit Start or Continue. Internal clock
commands are rejected unless Internal is selected.

External clock loss defaults to 500 ms and never falls back automatically.
`SequencerTimeline` derives step, beat, and bar boundaries from accepted pulses.
`BarQuantizedSwap<T>` activates the latest queued value at the next strictly
future bar, or immediately while stopped. It is a standalone utility;
`PatternPlayer` currently implements its own pending-swap logic.

## Current implementation limits

`TransportEngine.Position` reports the next pulse; `PulseAccepted` carries the
pulse being processed. The player's `Position.Bar + 1` deadline therefore skips
the upcoming boundary if queued between pulses 95 and 96. Start resets pulse
position but does not rebase a pending deadline from the previous run.

Transport invokes subscribers outside its state lock. Concurrent callers can
publish an earlier accepted pulse after a Stop notification, allowing the
player to start a note after stop cleanup. Per-object locking does not order
those notifications across threads.

Internal clock restart cancels the prior scheduler without awaiting completion
before replacing its task and cancellation source. A late failure from the old
run can cancel the new run. Scheduler faults have no dedicated live error
reporting path. Disposal cancels and awaits only the latest task, without
sending MIDI Stop to the selected output.

These lifecycle and quantization limits require focused changes before stronger
timing or shutdown guarantees can be made. See the [theory of
operation](../architecture/theory-of-operation.md) for execution contexts.

Related: [candidate workflow](../performance/candidate-workflow.md) and
[note lifecycle](note-lifecycle.md).
