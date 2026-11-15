# Follow-up code review

The core/console split, immutable materialized patterns, typed generator
settings, explicit composition, and separate port owners are suitable for the
current one-track application. No broad structural rewrite is justified.
The [theory of operation](../architecture/theory-of-operation.md) is useful as
the runtime entry point; retain it and keep its failure limits explicit.

The following defects have local reproductions using fake ports and the real
application classes, without native MIDI access. Address them in this order,
adding regression coverage with each fix. P1 identifies a control/liveness
failure under the stated condition; P2 identifies a narrower correctness issue.

## P1: internal clock startup can monopolize command processing

`InternalMidiClock.BeginAsync` calls its run synchronously while holding the
lifecycle gate. `StopwatchClockPulseScheduler` yields only when it is ahead of
its deadline. If each pulse takes longer than its interval, the loop stays
behind and never reaches an incomplete await. Start therefore cannot return,
and Stop/disposal cannot acquire the lifecycle gate to cancel it. The console
also cannot read another command while awaiting Start.

Reproduction: use the default scheduler at 300 BPM with a fake output whose
Clock send takes 15 ms. After four pulses, Start is still incomplete and Stop
waits behind it. Making the fake sends fast again allows both calls to finish.
This establishes the liveness gap, not a physical device latency measurement.

Keep startup and cancellation responsive under sustained overrun. Preserve
join-before-replacement and no-pulses-after-Stop contracts, and define how an
overdue scheduler catches up without monopolizing the caller. Test the default
scheduler as well as injected schedulers.

## P2: an owed MIDI Stop can reach a replacement output

The console's source command stops an internal run only when `IsRunning` is
true. A failed scheduler has already completed, but its successful Start still
leaves a MIDI Stop owed. Switching to External skips that Stop; a subsequent
output replacement also skips internal-clock Stop because External is selected.
Clock disposal later sends the owed Stop through the new output.

Reproduction: select output A, select Internal, Start with a failing scheduler,
select External, select output B, then quit. Recorded operations include
`A:Start, A:Dispose, B:Stop`, with no `A:Stop`.

Settle the clock's transport obligation before retiring its output,
independently of whether scheduling is still running. Cover scheduler failure,
source change, output replacement, shutdown, and Stop-send failure together.
An unused clock must continue to send no transport messages.

## P2: indented save/load commands corrupt the argument

Command tokenization removes leading whitespace, but save/load slice the
original line using only the command token's length. Two leading spaces make
`  save Review Indent` save the name `ve Review Indent`, and
`  load Review Indent` search for `ad Review Indent`.

Normalize the command span once and derive free-text arguments from that same
span. Test indented bare commands and multiword names through the full console
loop. Make argument-count checks consistent before side effects; most commands
currently ignore extra arguments while `vary` rejects them.

## P2: Turnaround generation and variation disagree about timing

`TurnaroundLoopGenerator.Generate` requires 16 source steps but accepts any
valid `PatternTiming`. Variation requires sixteenth notes. A source setting of
12 pulses per step therefore generates a 64-step, eight-bar pattern that its
own generator refuses to vary as a four-bar loop.

Reject unsupported source timing before generation, consistently with the
documented four-bar form and the Euclidean phrase generator. Test generation
and variation together. Preserve musical output and random-call order for the
existing supported sixteenth-note settings.

## Readability after correctness

- Extract named command operations and a small parsing boundary from
  `JamWeaverConsole` as parsing is fixed. Keep the explicit dispatch; a generic
  command framework or service container has no demonstrated benefit here.
- Expand dense groove/motif helper statements when those algorithms are
  touched. Keep versioned random-call sequences unchanged.
- Revisit the shared variation target's placement with the existing
  [generator-specific variation plan](generator-specific-variation.md), rather
  than relocating types during lifecycle work.

## Verification and remaining limits

The solution builds without warnings and both native xUnit runners pass
(301 core and 51 console tests). The four reproductions expose cases absent
from that suite; passing existing tests does not close these findings.
The review covers runtime coordination, MIDI ownership, command handling,
pattern/validation boundaries, persistence, generator interfaces, and the
architecture documentation. It does not establish musical quality or timing.

[Physical smoke coverage](../hardware/external-clock-validation.md) establishes
normal external-clock playback, clock-loss silence, paused behavior when pulses
return, recovery through Stop/Start, and shutdown. Continue, pending replacement
across restart, native replacement/failure recovery, and quantitative timing
remain separate hardware checks. No new hardware exercise is part of this review.
