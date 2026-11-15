# Codebase findings

Address the code review findings one at a time, preserving the core/console
split, immutable materialized patterns, and typed generator settings. Prefer
focused changes to lifecycle ownership and testing boundaries over a broad
architectural rewrite. Product behavior decisions belong to the human.

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

## Next: shutdown MIDI Stop

Quitting or EOF while internal clock runs cancels scheduling without sending
MIDI Stop. Define shutdown ordering while output is still alive, preserve note
and resource cleanup after errors, and test Stop before port disposal with no
later pulses. Hardware response to disconnects still needs physical testing.

## Pending candidate activation

`Position` reports the next pulse. The player's `Position.Bar + 1` deadline
skips the imminent boundary when queued between pulses 95 and 96. Start also
retains pending deadlines from the previous transport run.

- Define activation from a consistent pulse snapshot.
- Ask whether Start should activate pending material at pulse zero or at the
  following bar; preserve the selected policy in documentation and tests.
- Cover queueing around boundaries, before the first pulse, and across
  Stop/Start with differing pattern lengths and resolutions.
- Reconcile the standalone `BarQuantizedSwap<T>` only once policy is clear.

## Output cleanup and port ownership

`SafeMidiOutput` stops cleanup after its first failed send. A replacement
opened by the console can be left undisposed if old-port cleanup fails.
Input replacement also needs exception-safe ownership during construction and
listener startup.

- Attempt independent cleanup operations and aggregate contextual errors.
- Define ownership of proposed, adopted, and failed replacement ports.
- Test send/disposal failures, replacement failure, and input listening failure.

## Domain default values

Struct constructor validation does not prevent invalid `default(T)` values.
Pattern accepts zero timing, empty identity, or a default name; PatternNote
accepts a default gate. Some enums also lack aggregate validation.

- Enforce invariants consistently at aggregate/API boundaries or choose types
  that cannot represent those invalid values.
- Test defaults, invalid enum casts, and persistence round-trip for accepted
  domain values.

## Console discovery boundary

`ConsoleDisplay` directly enumerates native ports, including during command-loop
startup outside its try/finally. Native failure can prevent startup and leave
the external-message subscription attached. Command-loop tests depend on the
installed MIDI subsystem.

- Inject a small port catalog and pass device data to rendering.
- Include setup in lifetime/error handling.
- Test empty devices, discovery failure, port selection and replacement, and
  continued command processing without native MIDI access.

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
