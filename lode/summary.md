# Project summary

JamWeaver is a .NET 11 MIDI application for contributing generated patterns to
live jams. Its tested core library owns MIDI safety, clock and transport,
immutable patterns, deterministic generation, playback, candidate audition, and
JSON persistence. The console owns MIDI-device adapters and line-oriented
controls. Its command loop uses an injectable port catalog, while rendering
receives device-name lists. Discovery errors leave commands available; automated
console tests use fake ports and discovery without accessing native MIDI.
The code currently has no external users.

The [follow-up review](plans/follow-up-review.md) records regression boundaries
and deferred readability work; it has no open correctness findings.
The [maintainability review](plans/maintainability-review.md) tracks focused
console and shared-contract cleanup under scripted regression protection.
`verify.ps1` provides one local build/test entry point, shared by the prepared
GitHub workflow. SDK/analyzer policy and formatting preferences are checked in;
see [verification](development/verification.md) for scope and hosted-run limits.
An internal comparison owner handles matched phrase/groove preparation,
toggling, and its history coordination. Console token conversion and variation
grammar have focused parsing helpers; domain value validation remains in core.
The console parses command tokens and free-text names from one trimmed span
and rejects malformed argument counts before dispatch. Indented save/load
commands preserve names and support the same bare forms as unindented commands.
Internal-clock startup queues its owned run independently of the lifecycle
caller, and the default scheduler yields between overdue pulses without
discarding them. Stop and replacement still join the previous run.
Source and output changes settle any owed internal MIDI Stop on the previous
output even after a scheduler fault. A failed Stop aborts the transition and
preserves the obligation for retry or shutdown; an unused clock sends nothing
during cleanup.

Jam-session preferences are ergonomic defaults, not product boundaries. The
application should start in a context that is immediately useful for its regular
sessions while keeping its musical model and controls capable of other keys,
scales, styles, roles, and devices. In particular, A minor pentatonic is the
current startup preference rather than JamWeaver's musical identity.

The application currently plays one MIDI-routed track. Patterns may contain
several notes per step and can follow internal or external MIDI Clock. Changes
made during playback take effect at the next bar; a fresh MIDI Start plays any
waiting candidate from the restarted first pulse. A performer can generate,
mutate, audition, accept, reject, save, and load patterns without sending
device-specific patch data.

The default Euclidean generator creates concise one-bar melodic patterns in the
selected bass, middle, or high register. Console generation offers `euclidean`,
`turnaround`, `motif`, `phrase`, and `groove`. The Euclidean phrase experiment is
retired from console selection, including its `euclidean2` alias; its core
algorithm, recipe reconstruction, saved-pattern playback, and variation remain
supported.
The Turnaround Loop generator repeats a concise Euclidean source bar in the
form A, A, A, and A-turnaround. Its generator-specific variation preserves that
identity through mirrored loop edits and isolated final gestures. Generation
requires a 16-step sixteenth-note source bar, matching variation's four-bar
timing contract.
Motif, structured-phrase, groove-vocabulary, and core-only drum generators also
remain available for experimentation. Structured phrase supports one, two, or
four bars; a [focused plan](plans/revisit-phrase-generator.md) covers stronger
identity preservation, clearer controls, and consistent endings without
changing the current version-1 algorithm.
Generated material and recipes are deterministic and versioned; saved
materialized steps remain authoritative for playback. Each generator owns both
generation and variation, and mutation recipes retain the original generator
lineage so repeated variation returns to the same generator.
Variation settings share `VariationTarget` in the generation namespace, with
stable names and numeric values. The [generation
contract](generation/pattern-generation.md#mutation-and-recipes) records their
semantics; saved patterns and musical output remain compatible. Motif version 4
and groove version 1 have fixed material/recipe snapshots alongside their
behavioral tests, protecting their readable local generation operations.

The generic melodic path, note cleanup, internal and external clock, candidate
workflow, and save/restart/load have been exercised on physical devices.
The current console path has [external-clock smoke
coverage](hardware/external-clock-validation.md) for discovery, playback,
Stop/restart, live candidate replacement, clock-loss silence, paused behavior
after pulses return, recovery through Stop/Start, and clean shutdown.
Generator musical quality, quantitative timing, long-running behavior, drum
workflow, and device-specific controls still require further hardware work.

See [theory of operation](architecture/theory-of-operation.md),
[project structure](architecture/project-structure.md), [pattern
generation](generation/pattern-generation.md), [candidate
workflow](performance/candidate-workflow.md), and [pattern
library](persistence/pattern-library.md).
