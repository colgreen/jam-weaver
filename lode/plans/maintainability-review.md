# Maintainability review

The current core/console split, typed generators, immutable patterns, and explicit
resource ownership provide a suitable foundation for the one-track application.
The reviewed code supports focused cleanup around command coordination and shared
variation contracts. A solution-wide architectural rewrite has no demonstrated
benefit. This plan proposes structural work; it does not change musical or
performer-facing behavior.

## Starting point

Begin with stage 1's scripted console regression tests against the existing
implementation. Read the console parsing and candidate-workflow contracts, then
inspect the comparison and history paths and existing test fixtures before
describing the test cases. Keep production refactoring out of that first change.
Capture the motif snapshots before stage 4; they need not delay console cleanup.

Proceed to stage 2 once its behavioral protection passes. Treat the remaining
stages as separate focused tasks, preserving musical output and command behavior
unless a behavior change is explicitly agreed.

## 1. Protect the behavior being moved

`ConsoleCommandTests` exercises argument-count rejection, save/load, variation,
and generator selection through the real command loop. Core tests separately
exercise candidate acceptance, history, and playback activation. Successful
console `compare`, `back`, `forward`, and `revert` sequences lack equivalent
command-loop coverage.

Before moving those operations, add focused scripted-session tests for matched
comparison creation and toggling, browsing after selection, branching history,
and the interaction of candidate selection with the accepted safe point. Include
the comparison's bass-role restriction and relevant failures. Preserve existing
history ordering and activation rules; changing them is a separate behavior
decision. Use fake ports and deterministic seeds without native MIDI access.

`MotifGeneratorTests` checks same-seed repeatability, reconstruction, and musical
invariants, but has no fixed expected-output snapshot. Those comparisons can
still pass when both sides change together. Capture representative current
version-4 output before refactoring motif code, including roles and shapes.
Retain the behavioral assertions alongside snapshots.

## 2. Separate console responsibilities

`JamWeaverConsole.RunAsync` combines reading, dispatch, argument conversion,
control updates, comparison generation, history coordination, and presentation.
Its constructor takes 16 dependencies, including concrete phrase and groove
generators used for the comparison operation.

Start by extracting named operations from the dispatch switch. Keep the switch
explicit and preserve error recovery, startup cleanup, and the parser's rejection
of malformed counts before command effects. Move token-to-value conversion into
focused console parsing helpers, particularly the variation grammar; domain
invariants remain with their core owners.

Give matched-comparison preparation and toggling one focused owner rather than
keeping its two patterns and selection flag in the read loop. Keep candidate
selection and history coordination together where they represent the same
operation, while preserving comparison's intentional two-entry history behavior.
Do not move live activation policy out of `PatternPlayer` or accepted-safe-point
policy out of `CandidateSession`.

Introduce a separate coordinator only where the extracted operations share real
state and dependencies. Do not hide the constructor's dependencies in a generic
context object, add a service container, or create a command-handler framework
solely to shorten the loop. Keep port replacement and clock shutdown ordering
intact; they already have focused owners and regression tests.

## 3. Correct the shared variation vocabulary

`GeneratorVariationSettings.Target` exposes `PhraseMutationTarget`, declared
inside the phrase subsystem, although it is consumed by multiple generator
families and the console. A shared variation target belongs with the shared
variation contract. A focused rename/move can correct this dependency without
changing the available targets or their interpretation.

Review public API compatibility explicitly, retain serialized target names and
values, and preserve existing recipe IDs, versions, lineage, output, and
random-call order. Do not broaden this cleanup into replacing all phrase-named
controls without examining their actual semantics.

Family-specific mutation changes remain separate musical work under the
[generator-specific variation plan](generator-specific-variation.md).

## 4. Improve generator readability under compatibility coverage

`GrooveVocabularyGenerator` and `MotifGenerator` contain compressed statements,
nested expressions, and short implementation names that obscure musical rules.
Expand the relevant statements and name meaningful operations or constants.
Retain cohesive algorithms rather than introducing a generic generation pipeline.

Keep fixed-seed output, random-call order, recipe metadata, and generator versions
unchanged for a behavior-preserving refactor. Follow the [generator version
checklist](../development/adding-a-generator.md#version-change-checklist) if a
deliberate algorithm change is required.

## 5. Make routine verification easier to enforce

There is no checked-in `.editorconfig`, CI workflow, or explicit shared analyzer
policy. Add a small formatting policy consistent with the existing source and a
shared build policy for agreed diagnostics. Start with SDK-provided checks and
review their output before making additional diagnostics mandatory. Keep a
formatting sweep separate from structural changes.

Provide one documented local verification entry point for the solution build and
both native xUnit runners, with failures propagated. Add CI for the same commands
when the repository hosting and .NET 11 SDK availability are established. This
does not require a coverage-percentage target or extra analyzer packages.

Repeated console test setup may benefit from a small shared disposable fixture
as command tests grow. Share stable composition and cleanup, keeping specialized
fault-injection fakes local to the tests whose contracts they explain.

## Verification and scope

The reviewed baseline builds without warnings using
`dotnet build JamWeaver.sln --no-restore`. Both native xUnit runners pass: 324
core tests and 126 console tests. This is a starting baseline, not proof of
complete coverage or physical MIDI behavior.

Implement each stage as a focused change and review its design before source
edits. Run the solution build and both test runners after code changes. Update
the relevant current-state Lode contracts with the resulting ownership and
boundaries. Hardware timing, disconnect recovery, and musical quality remain
separate validation concerns.

Related: [project practices](../practices.md), [project
structure](../architecture/project-structure.md), [console
commands](../development/console-commands.md), and [follow-up
review](follow-up-review.md).
