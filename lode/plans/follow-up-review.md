# Follow-up code review

The core/console split, immutable materialized patterns, typed generator
settings, explicit composition, and separate port owners are suitable for the
current one-track application. No broad structural rewrite is justified.
The [theory of operation](../architecture/theory-of-operation.md) is useful as
the runtime entry point; retain it and keep its failure limits explicit.

The following defects have local reproductions using fake ports and the real
application classes, without native MIDI access. Add regression coverage with
each fix. The remaining P2 finding concerns generator timing validation.

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

- Retain the [console parsing boundary](../development/console-commands.md) and
  explicit dispatch. A generic command framework or service container has no
  demonstrated benefit here.
- Expand dense groove/motif helper statements when those algorithms are
  touched. Keep versioned random-call sequences unchanged.
- Revisit the shared variation target's placement with the existing
  [generator-specific variation plan](generator-specific-variation.md), rather
  than relocating types during lifecycle work.

## Verification and remaining limits

The solution builds without warnings and both native xUnit runners pass
(310 core and 120 console tests). Clock-liveness regression coverage checks
Start and Continue at 300 BPM with 15 ms fake Clock sends, direct default-scheduler
yielding during overrun, and startup/disposal with an inline injected scheduler.
The [clock contract](../midi/clock-transport.md#internal-source) retains every
pulse and joins callbacks before Stop or replacement. This establishes
responsiveness under the tested overload, not physical timing performance.
Clock-ownership coverage checks scheduler faults after Start/Continue, source
changes, direct output replacement, blocked transitions on Stop-send failures,
command and shutdown retries on the original output, and cleanup with an unused
clock. Owned cleanup joins callbacks, preserves cancellation rules, and leaves
external transport alone even when an internal Stop is owed.
Command-loop coverage checks indented bare save/load, multiword names, internal
spaces, numbered load, optional arguments, and count errors before MIDI,
port-opening, candidate, control, persistence, or random-seed effects.
The remaining reproduction exposes a case absent from the suite; passing
existing tests does not close that finding.
The review covers runtime coordination, MIDI ownership, command handling,
pattern/validation boundaries, persistence, generator interfaces, and the
architecture documentation. It does not establish musical quality or timing.

[Physical smoke coverage](../hardware/external-clock-validation.md) establishes
normal external-clock playback, clock-loss silence, paused behavior when pulses
return, recovery through Stop/Start, and shutdown. Continue, pending replacement
across restart, native replacement/failure recovery, and quantitative timing
remain separate hardware checks. No new hardware exercise is part of this review.
