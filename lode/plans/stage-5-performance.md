# Stage 5: pattern playback and candidate workflow

> Status: implemented and covered by the xUnit suite.

Stage 5 turns the existing pattern, generation, and transport components into a
single-track performance engine. It plays melodic and drum patterns, supports
private candidate audition and one-level undo, and adds key-finding controls.
Persistence remains Stage 6.

## Source structure

```text
src/JamWeaver.Core/Performance/
  PatternPlayer.cs
  CandidateSession.cs
  StepTriggerDecision.cs
  PatternTransformations.cs
```

The console composes these services with existing generators and transport. No
core type depends on terminal or DryWetMIDI APIs.

## Pattern playback

`PatternPlayer` subscribes directly to accepted transport pulses. It owns:

- The current and latest pending pattern.
- The performance MIDI channel.
- Whether note output is enabled (`play`) or muted (`mute`).
- Scheduled Note Off deadlines for notes it started.

At each pulse it performs a bounded sequence:

1. Send Note Off for every note whose gate deadline is this pulse.
2. At a bar boundary, activate the latest eligible pending pattern.
3. If enabled and this is the active pattern's step boundary, evaluate trigger
   probability and send the step's Note On messages.
4. Record each successful Note On and its Note Off deadline.

This explicit ordering retriggers consecutive identical notes cleanly. Pattern
timing and length are read from the activated immutable pattern, so a 12-step
pattern can loop independently of the 96-pulse bar.

Gate duration is `ceiling(pulsesPerStep * gate)`, clamped from one through the
full step length. A 100% gate therefore ends exactly on the next step boundary.
Drum and melodic notes use the same gate contract.

Melodic pitches resolve through the candidate's `TonalContext` and
`MusicalRole`. Drum pitches are already absolute MIDI note numbers.

## Trigger probability

Trigger decisions are stateless and deterministic. A stable mixer derives a
uniform value from:

- Pattern ID bytes.
- Pattern loop index since MIDI Start.
- Step index.

Probability zero never triggers and probability one always triggers without
consulting the mixer. Repeating MIDI Start reproduces the same decisions for the
same pattern. Continue preserves transport position and therefore the decision
sequence. This does not consume or perturb generator PRNG state.

## Candidate session

`CandidateSession` keeps three immutable pattern references:

- `Accepted`: the current safe baseline.
- `Candidate`: the latest requested audition pattern.
- `PreviousAccepted`: the single undo slot, when present.

The session may initially be empty. The first generated pattern becomes both
accepted and candidate so the performer has a usable baseline. Later generated,
mutated, transposed, palette, and role variants replace only the candidate and
are queued to the player.

Candidate playback changes are immediate while transport is not Running. While
Running, the latest requested change wins and activates at the next strictly
future bar. UI state can show both requested and currently audible pattern IDs.

Operations are:

- `accept`: immediately move the currently audible candidate into Accepted and
  move the old Accepted into PreviousAccepted. It rejects acceptance while a
  different candidate is still waiting for its bar boundary, preventing an
  unauditioned pattern from being accepted accidentally.
- `reject`: set Candidate back to Accepted and queue Accepted for playback.
- `undo`: restore PreviousAccepted as Accepted and Candidate, then queue it for
  playback; the displaced Accepted becomes the new undo value, allowing one-step
  toggling.

Reject and undo are bar-quantized only when they change audible playback.
Acceptance itself produces no MIDI or playback swap.

## Key finding and transformations

Melodic candidate transformations create new pattern snapshots and preserve
scale-degree steps:

- Root up/down wraps through pitch classes 0-11.
- Palette toggles major/minor pentatonic.
- Role changes to bass, middle, or high.

The console displays pitch-class names (`C`, `C#`, ... `B`) and the palette, but
selection remains by ear. Transformations reject drum patterns with a concise
error. Existing model rules give transformed snapshots new IDs and clear recipes
that no longer reproduce the transformed material.

## Note safety and failures

PatternPlayer maintains its own scheduled-note set in addition to
`SafeMidiOutput`'s global safety set. It silences its notes when:

- `mute` is requested.
- Transport leaves Running because of Stop, clock loss, or source change.
- Playback channel changes.
- The player is disposed.
- A playback send or pitch-resolution operation fails.

Silencing uses explicit Note Off for owned notes. `panic` remains the broader
all-channel safety operation. Candidate replacement does not cut a note early;
the old note completes its gate while the new pattern starts at the boundary.

Timing callbacks do not log, block, allocate unbounded work, or call arbitrary
UI handlers. The player captures the first playback error, disables playback,
silences owned notes, and exposes copied status for the host to report outside
the clock callback. Failure handling must not throw back through a device MIDI
input callback.

## Console workflow

Stage 5 adds:

```text
generate [seed]
mutate [seed] [strength-0..1]
accept | reject | undo
root up|down | palette | role bass|middle|high
channel <1..16>
play | mute | pattern
```

`play` enables pattern notes but does not send or change transport. With external
sync, the performer can enable audition before the next incoming step; edits
remain bar-quantized. `mute` releases notes immediately without stopping clock.

The initial `generate` command creates a 16-step melodic pattern using the
current tonal context and role. Defaults are bass, C minor pentatonic, moderate
density/movement, fairly high repetition, 80% gate, and modest velocity
variation. An omitted seed comes from a Redzen random source and is printed so
the result can be reproduced. Drum playback is supported by the engine, while a
friendly device/drum-note setup menu is deferred until routing needs are defined.

`pattern` reports accepted, candidate, audible/pending state, mode, seed when
available, role, friendly palette, channel, and play/mute status. Existing raw
note/CC commands remain available for diagnostics.

## xUnit verification

The Stage 5 xUnit tests cover:

- Exact Note On/Off pulse positions for fractional and 100% gates.
- Note Off before retrigger at a shared pulse.
- Simultaneous notes and independent gates.
- Melodic resolution and absolute drum note playback.
- Deterministic intermediate trigger probability and Start-position cleanup.
- Immediate stopped swaps and latest-wins strictly-future bar swaps.
- Play/mute behavior and note cleanup on every transport exit.
- Channel-change and disposal cleanup.
- Playback failure containment and surfaced error status.
- First candidate, queued candidate, accept protection, reject, and undo toggle.
- Root wrapping, palette toggle, role changes, and drum rejection.

Tests use fake MIDI output and manually delivered transport pulses. They do not
sleep, enumerate ports, or require hardware.

## Explicitly deferred

- JSON save/recall and saved-pattern menus (Stage 6).
- Multiple tracks, per-track mute, and MIDI routing matrices.
- Device-specific drum maps, CC/effects automation, and SysEx.
- Swing, chords generated by the initial UI, and automatic key detection.
- Terminal keypress/raw-mode UI; Stage 5 retains line commands.

## Resolved decisions

- Gates use pulse deadlines; Note Off precedes Note On at the same pulse.
- Accepted, candidate, and previous-accepted references provide reject and undo.
- Acceptance is immediate bookkeeping; audible changes are bar-quantized.
- Key finding uses chromatic roots, pentatonic palette toggle, and friendly names.
- Probability is deterministic from pattern identity and transport loop position.
- Play/mute controls pattern notes independently from transport.

Related: [candidate workflow](../performance/candidate-workflow.md),
[pattern model](../sequencer/pattern-model.md),
[clock and transport](../midi/clock-transport.md), and
[initial sequencer plan](initial-sequencer.md).
