# Simplify the console program

`JamWeaver.Console/Program.cs` currently combines application composition,
resource lifetime, interactive state, command parsing and dispatch, generation
configuration, candidate workflows, MIDI device and transport control,
persistence workflows, and console rendering. Its size is a symptom of these
multiple responsibilities rather than the problem by itself.

The target is a small composition root supported by focused console-layer
types. Core musical and MIDI behavior remains in `JamWeaver.Core`; terminal
commands and presentation remain in `JamWeaver.Console`.

## Intended structure

```text
Program
  compose and dispose dependencies
  create JamWeaverConsole
  call RunAsync

JamWeaverConsole
  run the read/dispatch loop
  coordinate command effects

GenerationControls
  hold performer-selectable generation state and defaults

CandidateGenerator
  map mode, controls, seed, and current context to a generated Pattern

ConsoleDisplay
  render prompts, setup, status, patterns, library entries, and help
```

Names are provisional until each extracted type's exact responsibility is
confirmed from the code. Prefer the shortest name that clearly states that
responsibility, following [project practices](../practices.md).

## Design boundaries

- `Program` is responsible only for composition, resource lifetime, and
  starting the application.
- `JamWeaverConsole` translates interactive commands into operations. It may
  remain substantial, but should not own generator algorithms or rendering
  details.
- `GenerationControls` is state, not a service. It holds generator mode and the
  current phrase, groove, and motif controls with their startup defaults.
- `CandidateGenerator` owns or receives the available generator implementations
  and constructs their strongly typed settings. It preserves current tonal
  context and role where supported.
- `ConsoleDisplay` performs terminal presentation without changing application
  state.
- Console-specific types remain outside `JamWeaver.Core`.
- Resource ownership and deterministic disposal remain explicit, especially for
  MIDI ports, clocks, playback, and cancellation resources.

The top-level command dispatcher remains explicit. Do not introduce one command
class per command or a runtime command registry unless a concrete second use
requires discovery or independent command composition.

## Stage 1: isolate generation configuration

Introduce `GenerationControls` and `CandidateGenerator` first. Replace the
current generation method's long parameter list with these cohesive types.

Preserve:

- Existing generator names and selection behavior.
- Startup defaults.
- Inheritance of tonal context and musical role from the current candidate.
- Generator-specific control applicability and validation.
- Deterministic seed behavior and generated recipe contents.

Add tests for generator-mode mapping, constructed settings, inherited context,
unsupported role or control combinations, and deterministic output. Prefer
testing observable patterns and recipes rather than implementation details.

## Stage 2: isolate console presentation

Move prompt, setup, status, pattern-grid, library-list, help, and domain-to-text
rendering into `ConsoleDisplay`. Decide whether it receives a `TextWriter` when
extracting it; use one if it materially simplifies deterministic tests without
obscuring normal `Console.Out` use.

Rendering methods must not mutate candidate, playback, transport, device, or
library state. Preserve existing user-facing text unless a deliberate interface
change is agreed.

Add focused tests for presentation logic where errors would affect usability,
especially pattern grids, generator-specific labels, and ambiguous library
entries. Avoid brittle snapshots of the entire help document when smaller
behavioral assertions suffice.

## Stage 3: extract the interactive application

Move the read/dispatch loop into `JamWeaverConsole.RunAsync`. Keep `Program` as
the composition root. Group private command handlers by responsibility when
that makes state and control flow clearer:

- Device and transport.
- Generation settings and candidate creation.
- Candidate navigation and transformation.
- Pattern library.
- Raw MIDI diagnostics.

Do not create handler interfaces solely to reduce file length. Extract another
type only when it has a coherent responsibility, independent tests, or a
concrete second consumer.

Preserve the current error boundary: command failures receive useful terminal
context without terminating the interactive session, while application startup
and disposal failures remain visible.

## Stage 4: review remaining responsibilities

After the first three stages, reassess `JamWeaverConsole` using the resulting
code rather than predicting further abstractions. Consider additional
extractions only for demonstrated concerns such as MIDI port switching or
pattern-library command coordination.

Review type names, XML summaries, and remarks against [project
practices](../practices.md). Put detailed theory of operation in focused Lode
documents rather than expanding XML documentation.

## Verification

Each stage must:

- Preserve existing user-visible and generated behavior unless a change is
  explicitly agreed.
- Add or update deterministic tests for the responsibility moved.
- Run `dotnet build JamWeaver.sln`.
- Run `dotnet run --project tests/JamWeaver.Core.Tests` and any console tests
  introduced by the refactor.
- Report hardware verification separately; structural refactoring does not
  establish physical MIDI behavior.
- Update the [developer primer](../development/developer-primer.md), [adding a
  generator](../development/adding-a-generator.md), and [project
  structure](../architecture/project-structure.md) when their described
  composition changes.

## Completion criteria

The plan is complete when `Program.cs` contains only composition, explicit
resource lifetime, and application startup; generation configuration and
console rendering have focused owners; the command loop has a clear single
purpose; and tests cover the extracted deterministic behavior without requiring
MIDI hardware.
