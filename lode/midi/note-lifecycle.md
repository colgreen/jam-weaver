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

On orderly application exit, internal clock disposal joins the scheduler,
stops local playback, and sends any MIDI Stop still owed by its earlier
Start/Continue. Output remains open for that operation and subsequent note
cleanup. Nested resource scopes still attempt player/output disposal when
clock shutdown throws. See [clock shutdown](clock-transport.md#shutdown).

`PatternPlayer` additionally tracks the notes and pulse deadlines it owns. It
releases them on gate expiry, mute, transport exit, channel change, playback
failure, and disposal. Repeated Start cleanup happens on the next pulse whose
position repeats or moves backward, rather than on the Start notification
itself. Note Off is processed before Note On at a shared pulse so 100% gates
retrigger cleanly.

Transport serializes mutations and notification delivery, so an in-flight pulse
finishes before a concurrent Stop, source change, or clock-loss transition
releases its notes. No earlier accepted pulse can arrive after that cleanup.
Failed MIDI sends can still prevent successful release on the physical device.
See [clock and transport](clock-transport.md) for notification contracts and
remaining scheduler-lifecycle limitations.

Related: [project practices](../practices.md).
