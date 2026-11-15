# Codebase findings

Address the code review findings one at a time, preserving the core/console
split, immutable materialized patterns, and typed generator settings. Prefer
focused changes to lifecycle ownership and testing boundaries over a broad
architectural rewrite. Product behavior decisions belong to the human.
Commit each completed task after verification; the full review follows the
individual fixes.

All seven priority findings below have implementation and regression coverage.
The full review and later readability work remain separate follow-up work.

## Transport notification contract

Transport mutations and their synchronous notifications are serialized by a
dispatch gate; state reads use a separate lock. In-flight pulses finish before
Stop, source change, and clock-loss cleanup. Subscribers may read state but
cannot initiate reentrant mutations. Regression tests cover those races,
available state reads, reentrancy rejection, and gate release after failure.

See [clock and transport](../midi/clock-transport.md#notification-ordering).

## Internal clock run ownership

`InternalMidiClock` serializes asynchronous lifecycle methods. Restart cancels
and joins the old run before replacement; Stop joins before sending MIDI Stop.
Each run retains its token until joined, and fault cleanup does not cancel a
replacement or acquire the lifecycle gate. Scheduler/pulse errors stop local
transport and remain visible as `ClockError` in `status`. Explicit retry clears
the previous error at the MIDI send stage. Stop and disposal retain errors.

Tests cover held runs, late and synchronous failures, concurrent commands,
caller cancellation, Continue position, note release, failed sends, and
idempotent disposal. See [clock and transport](../midi/clock-transport.md).

## Shutdown MIDI Stop

Clock disposal joins scheduling and sends a Stop owed by its earlier successful
Start/Continue. Explicit Stop clears that obligation only after a successful
send. Shutdown keeps player and output alive through clock disposal, and nested
resource scopes attempt later cleanup after failures. Unused clocks send no
transport messages; an external-clock-only session is not relayed.

Tests cover quit/EOF, Stop before port disposal, no later pulses, failed sends
with continued note/port cleanup, background failure, retry after failed explicit
Stop, and duplicate/unused disposal. Hardware response still requires physical
testing. See [clock shutdown](../midi/clock-transport.md#shutdown).

## Pending candidate activation

The player reads one `TransportSnapshot` containing source, state, next pulse,
and accepted Start count. A running queue targets the first bar boundary whose
clock has not been accepted, including an upcoming boundary at the exact next
pulse. Start plays a waiting candidate on pulse zero; Continue preserves its
deadline and phrase phase. Acceptance still requires `keep`. `PatternPlayer`
owns the sole live activation policy.

Tests cover boundaries and delayed callbacks, repeated Start, Stop/Continue,
clock loss and source selection, muted output, acceptance, and differing lengths
and resolutions. See [pending pattern
activation](../midi/clock-transport.md#pending-pattern-activation).

## Output cleanup and port ownership

`SafeMidiOutput` attempts every note/channel cleanup operation, retains failed
Note Off for retry, and aggregates contextual send/disposal errors. Proposed
ports transfer ownership even on replacement failure. Cleanup failure preserves
the previous output; previous-port disposal failure clears selection. The
console stops internal transport on the old output before replacing it, and
panic attempts safe-output cleanup even when player mute fails.

Console `MidiInput` owns an `IMidiInputPort` selection. Replacement closes the
old listener before starting another; partial startup and teardown always
attempt detachment, stop, and disposal. Failed selection does not retain a
disposed input, and retired-port callbacks are ignored. Tests cover these
paths with fakes; native behavior still needs physical verification. See
[port ownership](../midi/port-ownership.md).

## Domain default values

Aggregates and consumption boundaries validate invalid struct defaults while
preserving valid zeros. Patterns, notes, recipes, generator settings, MIDI
routing/output, and timelines enforce their input invariants before storing
state or performing I/O. Transport, pitch resolution, variation, and groove
entry points reject undefined enums. Internal tempo is finite and 20-300 BPM.

Encoding and decoding share supported-schema and document-size limits. Tests
cover invalid defaults/enums, unchanged state and MIDI output on rejection,
generator entry validation, valid-zero round trips, and persistence rejection.
Fixed-seed tests retain their existing musical results. See [domain value
validation](../sequencer/value-validation.md).

## Console discovery boundary

`IMidiPortCatalog` separates discovery/opening from command coordination.
`Program` injects the native adapter; `ConsoleDisplay` renders supplied names
without device I/O. Input/output discovery errors are reported independently,
commands remain available, and later listing commands retry discovery.
Startup setup runs inside the loop's cleanup scope so escaping errors still
detach the external-message bridge and close input.

Command-loop tests inject a fake catalog and cover empty devices, discovery
failure/recovery, port selection/replacement, open/listen failures, continued
commands, and startup/reader failure cleanup without native MIDI access.
See [discovery and opening](../midi/port-ownership.md#discovery-and-opening).

## Later readability work

Extract named operations and parsing from the large console dispatch method as
those areas change. Handle argument counts and indented save/load commands
consistently. Expand dense groove/motif helpers when touched; preserve versioned
random-call sequences during any consolidation. The shared variation target
currently lives under phrase generation; revisit its location with the existing
[generator-specific variation plan](generator-specific-variation.md).

Runtime behavior and current limits are described in the [theory of
operation](../architecture/theory-of-operation.md). Verification requires a
clean build and both native xUnit runners; automated fake-port coverage does
not establish physical MIDI timing or disconnect recovery.
