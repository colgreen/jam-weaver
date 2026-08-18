# Repository instructions

This repository contains experiments for controlling MIDI devices from .NET/C#.
The human owns the code and makes final product and design decisions. Agents are
responsible for careful implementation, verification, and maintaining useful
project knowledge.

## Start of work

1. Read `lode/lode-map.md`, `lode/terminology.md`, and `lode/summary.md` when
   they exist.
2. Use `lode/lode-map.md` to find relevant design documentation before broadly
   searching the source tree.
3. Inspect the actual code involved before proposing or making a change. Code is
   the source of truth when documentation and implementation disagree.
4. State important assumptions and discuss the intended design before making
   implementation changes.

## Project knowledge (Lode)

Persistent project knowledge belongs in the AI-maintained `lode/` directory.
Keep it accurate enough that a future session can understand the current system
without reconstructing decisions from chat or commit history.

Expected structure:

```text
lode/
  summary.md        # concise living snapshot of the system
  terminology.md    # short "term - meaning" domain definitions
  practices.md      # project-specific engineering practices
  lode-map.md       # hierarchical index of persistent documentation
  plans/            # active roadmaps and plans
  tmp/              # disposable session notes and handovers; git-ignored
  <domain>/         # focused documentation for a subsystem
```

Create missing Lode files or directories when a task produces knowledge that
needs them. Keep `lode/lode-map.md` synchronized with the files it indexes.

Lode documents must:

- Describe the current state, not narrate completed work or duplicate a
  changelog.
- Record contracts, invariants, rationale, limitations, and lessons that affect
  future implementation.
- Cover one focused topic per file and use kebab-case filenames.
- Link related documents with relative links.
- Stay below 250 lines; split a larger document into focused files.
- Include examples or Mermaid diagrams only when they materially clarify the
  topic. Do not add ceremonial examples or diagrams.

Temporary investigation notes and requested session handovers go in
`lode/tmp/`. A handover should record current task state, decisions, attempted
approaches, blockers, and next steps. Do not promote transient debugging details
into permanent project documentation.

After changing behavior, architecture, public interfaces, or important project
conventions, update the relevant Lode documents in the same task. Documentation
must reflect the resulting system, not the sequence of edits used to reach it.

If Lode documentation conflicts with the code, report the discrepancy, follow
the code for the immediate task, and update the documentation when the correct
state is clear. Ask the human when resolving it requires a product or design
decision.

## Engineering approach

- Use chat for exploration and design before implementation. Do not jump
  directly to changing code.
- Implement only after the intended behavior and approach are clear and the
  human has made or confirmed the relevant decisions.
- Prefer the smallest coherent change that fully addresses the request.
- Keep MIDI protocol handling separate from console/UI concerns as the program
  grows. Isolate device I/O, clock/transport, message construction, and command
  parsing behind testable boundaries.
- Treat MIDI channel numbers shown to users as 1-16 and convert to any zero-based
  library representation only at the boundary.
- Validate all 7-bit MIDI data as 0-127. Document any device-specific mappings,
  NRPN sequences, or SysEx formats and their firmware/model assumptions.
- MIDI Clock is 24 pulses per quarter note. Avoid blocking device callbacks and
  avoid doing console output or other slow work on timing-critical paths unless
  it is explicitly diagnostic.
- Desktop .NET is not a hard real-time environment. Be explicit about timing and
  jitter limitations; measure before claiming performance characteristics.
- Dispose MIDI ports and cancellation resources deterministically. Handle device
  disconnects without leaving notes sounding or background clock tasks running.
- Do not silently swallow failures. Give errors enough context to identify the
  port, operation, or MIDI message involved without flooding normal output.
- Do not introduce abstractions for hypothetical requirements. Refactor when a
  boundary has a concrete second use or is needed for testing.

## C# conventions

- Target the framework declared by the project; do not retarget it incidentally.
- Keep nullable reference types enabled and resolve warnings rather than
  suppressing them without justification.
- Prefer clear domain names over abbreviations, except for established MIDI
  terms such as CC, NRPN, PPQN, and SysEx.
- Use asynchronous APIs for cancellable long-running work. Never use `async void`
  except for framework-required event handlers.
- Pass `CancellationToken` through operations that can wait or run continuously.
- Keep public APIs small and make ownership/disposal responsibilities explicit.
- Pin package versions. Check release notes and compatibility before upgrading a
  MIDI or native-I/O dependency.

## Verification

For code changes, run at minimum:

```powershell
dotnet build
```

Run tests when a test project exists. Add automated tests for deterministic
logic such as message validation, channel conversion, command parsing, tempo
math, and external-clock estimation. Hardware-dependent behavior should sit
behind interfaces so it can be exercised with fakes; also state clearly when a
change still requires testing with a real MIDI device.

Do not claim that hardware behavior was verified unless the relevant physical
device was actually exercised. Report the build/test result and any remaining
hardware verification separately.

## Scope and safety

- Preserve user changes and avoid unrelated rewrites.
- Do not commit, publish, or contact external systems unless explicitly asked.
- Never include secrets, machine-specific device identifiers, or private paths
  in committed examples or documentation.
- Prefer standard MIDI messages for experiments. Treat device-specific SysEx,
  firmware updates, patch replacement, and bulk transfers as higher-risk actions:
  validate lengths and checksums, identify the exact target model, and require
  explicit intent before sending them to hardware.
