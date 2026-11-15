# MIDI note lifecycle

The application owns every note that it starts. A successful Note On is added
to the active-note set; its Note Off removes it. Timed notes are asynchronous
operations whose caller waits for completion. Cancellation still sends Note Off
through a `finally` block.

Before switching or disposing an output, the application attempts explicit Note
Off messages for tracked notes and then CC 123 (All Notes Off) on all 16
channels. Panic attempts the same sequence. `SafeMidiOutput.SilenceUnsafe`
currently exits on its first send failure, so remaining cleanup messages are
not attempted. Disposal still disposes the port in a `finally` block and
propagates the error. Replacement leaves the old port selected if cleanup
fails; the proposed new port has not been adopted or disposed by the wrapper.

This is a safety net rather than a replacement for correct scheduling: normal
playback must issue its corresponding Note Off at the intended gate time.

`PatternPlayer` additionally tracks the notes and pulse deadlines it owns. It
releases them on gate expiry, mute, transport exit, channel change, playback
failure, and disposal. Repeated Start cleanup happens on the next pulse whose
position repeats or moves backward, rather than on the Start notification
itself. Note Off is processed before Note On at a shared pulse so 100% gates
retrigger cleanly.

These are per-object cleanup paths, not a global ordering guarantee. A pulse
accepted concurrently with Stop can reach the player after its cleanup and
start another note. See [clock and transport](clock-transport.md) for current
notification-ordering and scheduler-lifecycle limitations.

Related: [project practices](../practices.md).
