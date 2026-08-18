# MIDI note lifecycle

The application owns every note that it starts. A successful Note On is added
to the active-note set; its Note Off removes it. Timed notes are asynchronous
operations whose caller waits for completion. Cancellation still sends Note Off
through a `finally` block.

Before switching or disposing an output, the application sends explicit Note
Off messages for all tracked notes and then sends CC 123 (All Notes Off) on all
16 channels. Panic performs the same sequence. Cleanup errors during shutdown
are reported, but do not prevent the port from being disposed.

This is a safety net rather than a replacement for correct scheduling: normal
playback must issue its corresponding Note Off at the intended gate time.

`PatternPlayer` additionally tracks the notes and pulse deadlines it owns. It
releases them on gate expiry, mute, transport exit, channel change, repeated
Start, playback failure, and disposal. Note Off is processed before Note On at a
shared pulse so 100% gates retrigger cleanly.

Related: [project practices](../practices.md).
