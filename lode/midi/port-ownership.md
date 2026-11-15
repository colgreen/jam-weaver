# MIDI port ownership

The console opens native ports, but dedicated owners control their lifetime.
`SafeMidiOutput` owns the selected `IMidiOutputPort`; console `MidiInput` owns
the selected `IMidiInputPort`. Passing a non-null proposed port to either
`ReplacePort` transfers responsibility for its disposal immediately, including
failure paths. Callers must not also dispose a transferred port.

Both owners treat replacement by the exact selected instance as a no-op.
They cache the selected name, avoiding native name access during failure
cleanup and after disposal. A proposal whose name cannot be read is disposed
before the existing selection is disturbed. A proposal passed to a disposed
owner is also disposed; if that disposal fails, both errors are reported.

## Discovery and opening

`IMidiPortCatalog` supplies name snapshots and opens ports by zero-based device
index. `Program` injects the native `DryWetMidiPortCatalog`; the command loop
uses the interface and passes names to `ConsoleDisplay`. Listing does not
select a port or start listening. Indices may change when devices connect or
disconnect; a displayed index does not reserve a device.

Setup discovers inputs and outputs independently. A discovery failure reports
its direction and underlying error, while other discovery and commands remain
available. Later `setup`, `out`, or `in` commands retry discovery. Empty lists
are displayed explicitly and are distinct from discovery failures. Startup
setup shares the command loop's try/finally, which detaches the external-message
bridge and closes the selected input on quit, EOF, or an escaping error.

Successful open returns a caller-owned port, with inputs initially not
listening. The console immediately transfers it to the appropriate selection
owner. Open failures are handled as command errors; an output open failure
retains the old selection after muting/stopping it, while an input open failure
leaves the old listener running.

## Output replacement

Before adopting a proposal, `SafeMidiOutput` attempts explicit Note Off for
tracked notes and CC 123 on all sixteen channels of the previous output. Each
send failure records the output, operation, channel, and note where relevant;
other cleanup operations still run. Successful Note Off removes that note from
tracking. Failed Note Off remains tracked for a later panic or cleanup retry,
even if CC 123 succeeds, because that message does not prove device silence.

If cleanup fails, the previous output remains selected and undisposed, and the
proposal is disposed. If cleanup succeeds, the owner detaches the previous port
before attempting its disposal. A disposal failure leaves no output selected,
disposes the proposal, and reports the failure; it does not retain a possibly
disposed port as usable. Another selection can recover the owner afterward.

The console mutes the player and stops internal transport on an existing
output before opening its replacement. This joins clock production and sends
MIDI Stop while the previous port is available. The new output does not
implicitly restart playback; use `go` after selection.

Output disposal attempts all cleanup messages and native disposal, aggregates
their failures, clears selection/tracking, and marks the owner disposed even
when native disposal fails. Repeated disposal does not repeat failed native
operations. A reported disposal failure means physical closure is unconfirmed.

See [note lifecycle](note-lifecycle.md) for cleanup and panic behavior and
[clock shutdown](clock-transport.md#shutdown) for output/clock ordering.

## Input selection and listening

`IMidiInputPort` exposes a name, classified real-time messages, listening
start/stop, and disposal. Events use the port instance as sender.
`DryWetMidiInput` maps native events into this interface; it does not forward
directly to transport. Opening a native adapter does not start listening.

`MidiInput.ReplacePort` closes the previous selection before subscribing and
starting the proposal. Opening the proposal happens before this call, so an
open failure leaves the existing listener untouched. Previous-port teardown
clears selection first, then independently attempts event detachment, listener
stop, and disposal. If any stage fails, the proposal is disposed without being
started and no input remains selected.

If proposal subscription or listener startup fails, the owner clears its
selection and attempts detachment, stop, and disposal of that partially started
port. Startup and cleanup errors are preserved together. Name and status never
refer to a disposed former selection after those failures.

Only events whose sender is the current selected port are forwarded to
`ExternalMidiClockInput`. Captured callbacks from a retired port are ignored.
Replacement/disposal are serialized on the command path; callbacks do not take
the selection lock. A separate forwarding gate lets an in-flight message finish
before selection is cleared; it is released before native stop/disposal, so a
native stop can wait for callbacks without a lock cycle. Reading the cached
name does not wait for native listening shutdown. Do not request replacement
or disposal inside a MIDI callback: a native stop may wait for that callback
to finish.

Input disposal attempts every teardown stage, clears selection, and becomes
idempotent even on failure. The native adapter detaches its device handler
during stop and disposal; device disposal is still attempted if detachment
fails. Hardware closure or silence cannot be guaranteed when native calls fail.

## Verification boundary

Fake-port tests exercise partial cleanup, failure aggregation, rejected-port
disposal, partially started listeners, late callbacks, idempotence, and panic
coordination. Command-loop tests use a fake catalog for empty/failed discovery,
retry, selection/replacement, continued commands, and cleanup after startup or
reader failure. Native discovery, disconnects, opening, and listening still
need physical testing.
No quantitative timing or physical disconnect-recovery guarantee follows from
the automated tests.
