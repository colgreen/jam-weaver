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
Tempo must be finite and within 20-300 BPM; invalid assignments preserve the
previous tempo. Undefined clock-source and real-time-message enums are rejected
before transport state changes or input forwarding.

`InternalMidiClock.StartAsync`, `ContinueAsync`, `StopAsync`,
`StopOwnedTransportAsync`, and disposal share one asynchronous lifecycle gate.
Start cancels and joins the previous run before sending another Start and
resetting position. Continue preserves
position and does nothing while its scheduler is already running. Stop joins
the scheduler before sending MIDI Stop; local transport is stopped even if
that send fails. The console awaits these operations, including when enabling
playback with `go`.

Source selection and output replacement first await `StopOwnedTransportAsync`.
It joins scheduling and sends only a still-owed MIDI Stop, regardless of
`IsRunning` or selected source. Scheduler failure therefore cannot hide a
transport obligation. An unused clock sends nothing during these transitions;
the explicit Internal `stop` command still sends Stop unconditionally.
Cleanup processes only local Internal Stop, leaving external transport alone.
A Stop-send failure reports the output and operation, preserves the obligation,
and aborts the transition before selecting a source or opening a replacement
port. The old output remains selected for command retry or shutdown cleanup.
Callers borrowing `SafeMidiOutput` must keep that output selected until this
operation succeeds; the clock does not own output selection.

Each run owns its cancellation token and remains joined before its source is
disposed or replaced. The pulse callback checks that run's token before
sending. The run is queued on the thread pool, independently of the lifecycle
caller, so even an injected scheduler that blocks before returning its task
cannot hold startup inside the lifecycle gate. Start/Continue complete after
queuing the run; scheduler failures may become visible in `Error` afterward.
Direct MIDI Start/Continue sends and local transport notifications still finish
before the run is queued.

The default scheduler retains absolute deadlines and emits every pulse. It
yields after each overdue pulse instead of monopolizing its caller while
catching up. It does not discard pulses or reset the deadline to hide overruns;
sustained overload can accumulate lateness. This is a liveness policy, not a
timing guarantee. Cancellation is checked before the next pulse, and Stop joins
any in-flight callback before sending MIDI Stop.

`IClockPulseScheduler` implementations must not complete their task
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

An unused clock sends no Stop during cleanup, including in an external-clock-only
session.
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
`PatternPlayer` owns pending pattern activation; it is the single implementation
of the live swap policy.

`TransportEngine.Snapshot` captures source, state, position, and accepted Start
count under one state lock. Position denotes the next pulse, while
`PulseAccepted` carries the pulse being processed. Start increments the count
and resets position; Stop, Continue, clock loss, and source selection preserve
the count. Consumers can distinguish a reset from continuation without guessing
from a backward pulse or combining separately read state and position.

## Pending pattern activation

While running, Queue retains only the latest pending pattern and records the
snapshot's Start count and first bar boundary whose clock has not yet been
accepted. If position is exactly at a boundary, that upcoming pulse qualifies:
a queue between accepted pulse 95 and upcoming pulse 96 activates at 96.
If pulse 96 was already accepted, the queue targets 192 even when delivery of
the earlier callback is delayed. Before the first clock, pulse zero qualifies.

A pending pattern from an earlier Start count activates on pulse zero after a
new Start, beginning at its own first step. It does not wait through a bar of
the old pattern. Acceptance remains separate bookkeeping through `keep`.
Continue preserves the pending boundary and absolute musical phase, including
after Stop, clock loss, or source selection. Ordinary swaps join absolute
transport phase; a four-bar pattern need not enter at its own first bar.

Stopping does not itself activate an existing pending pattern. Newly queueing
while stopped or clock-lost applies immediately and clears any earlier pending
selection. Activation bookkeeping still occurs while note output is muted.

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

Related: [candidate workflow](../performance/candidate-workflow.md) and
[note lifecycle](note-lifecycle.md).
