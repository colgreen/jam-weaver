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

`InternalMidiClock.StartAsync`, `ContinueAsync`, `StopAsync`, and disposal share
one asynchronous lifecycle gate. Start cancels and joins the previous run
before sending another Start and resetting position. Continue preserves
position and does nothing while its scheduler is already running. Stop joins
the scheduler before sending MIDI Stop; local transport is stopped even if
that send fails. The console awaits these operations, including when changing
source or enabling playback with `go`.

Each run owns its cancellation token and remains joined before its source is
disposed or replaced. The pulse callback checks that run's token before
sending. `IClockPulseScheduler` implementations must not complete their task
until all their callbacks have finished. Caller cancellation can cancel waiting
for the lifecycle gate; once admitted, an operation finishes joining its run
even if the caller's token is subsequently cancelled.

Scheduler and pulse failures are caught by the run, stop local transport, and
remain in `InternalMidiClock.Error`, displayed as `ClockError` by `status`.
Expected cancellation with the run's own token is quiet; unrelated cancellation
and an unrequested scheduler exit are failures. Direct start/continue/stop send
errors still reach command error handling. A start/continue attempt that reaches
the MIDI send stage clears the previous retained error as an explicit retry.
Stop and disposal retain errors. Fault cleanup never acquires the lifecycle
gate or cancels a shared replacement source. MIDI sends and subscribers can
still block; cancellation does not interrupt an in-progress native call.

Clock callbacks and scheduling paths must not perform blocking UI or persistence
work. Pattern changes are prepared off the timing path and swapped atomically at
the next bar boundary.

## Shutdown

`InternalMidiClock` remembers a successfully sent Start or Continue until a MIDI
Stop succeeds. Disposal cancels and joins its run, then sends any owed Stop
while the borrowed output remains open. A successful explicit Stop prevents a
duplicate on disposal; a failed explicit Stop leaves a disposal retry due.
Background scheduler failure and failure of local Start processing do not
erase the obligation to stop the receiving device.

An unused clock sends no Stop, including in an external-clock-only session.
An owed Stop belongs to this clock's own earlier Start/Continue, independently
of the source selected at disposal; it is not a relay of external messages.
Disposal is idempotent. A shutdown Stop failure is retained in `Error` and
propagated with output/operation context; repeated disposal does not repeat
the failed shutdown attempt.

Program keeps output and player alive while disposing the internal clock.
It then disposes the watchdog, player, and output. Nested `using` scopes attempt
later resource cleanup even if clock shutdown fails. The command loop closes
its input on quit, EOF, or loop failure before these outer resources unwind.
This is cooperative shutdown; forced process termination cannot promise MIDI
Stop or note release.

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

## Notification ordering

Transport mutations and their synchronous notifications share one dispatch
gate. A concurrent Stop, source change, Start, or clock-loss check waits for
an in-flight pulse's subscribers to finish before changing transport state
and notifying subscribers. A successful Stop therefore completes note cleanup
after earlier accepted pulses have been delivered. Subsequent pulses are
rejected until Start or Continue enables advancement again.

The separate state lock protects source, state, timestamps, and position. It is
released before calling subscribers; property reads do not acquire the
dispatch gate. This separation lets player commands query transport while a
notification is waiting for the player's lock, without a lock cycle.

Subscribers run on the calling thread and must stay short. They may read
transport state, but may not initiate another transport mutation or wait for
another thread to do so. Same-thread reentrant mutations throw
`InvalidOperationException`, preventing nested events from overtaking the
remaining subscribers. Subscriber exceptions propagate to the caller; the
dispatch gate is released even when a subscriber fails.

## Current implementation limits

`TransportEngine.Position` reports the next pulse; `PulseAccepted` carries the
pulse being processed. The player's `Position.Bar + 1` deadline therefore skips
the upcoming boundary if queued between pulses 95 and 96. Start resets pulse
position but does not rebase a pending deadline from the previous run.

These pending-swap limits concern musical activation timing. See the [theory
of operation](../architecture/theory-of-operation.md) for execution contexts.

Related: [candidate workflow](../performance/candidate-workflow.md) and
[note lifecycle](note-lifecycle.md).
