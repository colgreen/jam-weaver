# Maintainability review

The current core/console split, typed generators, immutable patterns, and explicit
resource ownership provide a suitable foundation for the one-track application.
The reviewed code supports focused cleanup around command coordination and shared
variation contracts. A solution-wide architectural rewrite has no demonstrated
benefit. This plan proposes structural work; it does not change musical or
performer-facing behavior.

## Starting point

Stage 1's scripted console regression coverage is in place against the existing
implementation, without production refactoring. Read the console parsing and
candidate-workflow contracts and inspect the protected paths before structural
changes. Motif and expanded groove snapshots protect stage 4's readability
changes and retain their pre-refactor expected output.

Stage 2 has named operations for comparison, browsing, and safe-point commands.
`CandidateComparison` owns comparison preparation, toggling, and its history
coordination.
`ConsoleArgumentParser` and `VariationArguments` own token conversion and the
variation grammar. Stage 3 uses the shared `VariationTarget` contract with
stable target names, numeric values, and saved output. Stage 4's motif and groove
implementations use explicit musical operations under fixed snapshot coverage.
Stage 5 has a shared SDK/analyzer policy, formatting preferences, and one local
verification entry point used by the prepared GitHub workflow. The local plan
is implemented; hosted CI execution remains to be confirmed after push. Preserve
musical output and command behavior unless a behavior change is explicitly agreed.

## 1. Protect the behavior being moved

`ConsoleCommandTests` exercises argument-count rejection, save/load, variation,
generator selection, and candidate workflow through the real command loop.
Scripted checkpoints protect matched comparison settings and seeds, toggling,
phrase-then-groove history ordering, browsing after selection, branching through
`new` and replacement comparisons, navigation limits, and the single accepted
safe point through `keep` and `revert`. Failure cases protect invalid seeds,
bass-only creation, and reuse of an existing pair after failed replacement.
Running-session coverage supplies external pulses directly to verify pending
acceptance rejection and next-bar activation without sleeps or native MIDI.
Core tests separately exercise candidate acceptance, history capacity, and
playback activation. Preserve existing history ordering and activation rules;
changing them is a separate behavior decision.

`MotifGeneratorTests` checks same-seed repeatability, reconstruction, and musical
invariants. `GeneratorSnapshotTests` adds twelve fixed version-4 motif snapshots
covering every shape, all roles, both palettes, and varied controls, plus seven
complete groove snapshots covering automatic and explicit selections. The
snapshots normalize only pattern identity and save time and retain all musical
and recipe values. Behavioral assertions remain alongside snapshots.

## 2. Separate console responsibilities

`JamWeaverConsole.RunAsync` combines reading, dispatch, control updates, and
presentation. Named private operations handle comparison
preparation/toggling, previous/next history selection, and `keep`/`revert`, along
with the existing source/output selection, save/load, and variation operations.
An internal `CandidateComparison` owns its prepared pair and toggle selection,
along with comparison-specific history addition, candidate selection, and
responses. The console only parses the optional seed and delegates the command.
Its public constructor takes 16 dependencies and constructs
the comparison owner from the existing generators and shared session services.

Keep the switch explicit and preserve error recovery, startup cleanup, and the
parser's rejection of malformed counts before command effects.
`ConsoleArgumentParser` owns numeric, MIDI, enum, and named-choice conversion.
`VariationArguments` parses target, strength, and named seed into typed
`GeneratorVariationSettings`. Domain value types retain invariant validation.
Command-loop tests protect accepted syntax, materialized variation output,
choice/value failures, and seed consumption before strength-range validation.

Keep comparison preparation, toggling, and its intentional two-entry history
behavior with `CandidateComparison`. Browsing and `keep`/`revert` remain named
console operations. Live activation policy stays in `PatternPlayer` and
accepted-safe-point policy stays in `CandidateSession`.

Introduce a separate coordinator only where the extracted operations share real
state and dependencies. Do not hide the constructor's dependencies in a generic
context object, add a service container, or create a command-handler framework
solely to shorten the loop. Keep port replacement and clock shutdown ordering
intact; they already have focused owners and regression tests.

## 3. Correct the shared variation vocabulary

`GeneratorVariationSettings.Target` exposes `VariationTarget` in the shared
generation namespace. The console, Turnaround Loop, and `PhraseMutationSettings`
use that contract. All five target names and numeric values remain fixed, and
the algorithms retain their existing interpretation and random-call order.
Serialized compatibility snapshots cover all targets through shared targeted
mutation and Turnaround Loop variation, including material, recipes, ancestry,
and lineage. Existing validation and command-loop tests remain in place.

There are currently no external users of the code. Settings target properties,
constructors, and deconstruction use the shared type, and no legacy enum
compatibility wrapper is needed. Saved format, target text, recipe IDs and
versions, lineage, and output are unchanged. See [pattern
generation](../generation/pattern-generation.md#mutation-and-recipes).
Other phrase-named controls and concrete algorithm types retain their existing
names and semantics.

Family-specific mutation changes remain separate musical work under the
[generator-specific variation plan](generator-specific-variation.md).

## 4. Improve generator readability under compatibility coverage

`GrooveVocabularyGenerator` and `MotifGenerator` use explicit statements and
musical names for source and developed bars, pitches, structural/ghost notes,
and recipe construction. Timing dimensions are named constants. Motif rhythm,
contour, and velocity rules and groove activity, pitch, and articulation rules
have focused local operations. Each family retains its own cohesive algorithm;
there is no generic generation pipeline.

The serialized snapshots were captured against the preceding implementations.
They protect complete material and recipe metadata in addition to the existing
behavioral, reconstruction, and category tests. Motif stays at version 4 and
groove at version 1, with unchanged fixed-seed output and random-call order.

Keep fixed-seed output, random-call order, recipe metadata, and generator versions
unchanged for a behavior-preserving refactor. Follow the [generator version
checklist](../development/adding-a-generator.md#version-change-checklist) if a
deliberate algorithm change is required.

## 5. Make routine verification easier to enforce

`.editorconfig` sets a small formatting policy consistent with the existing
source. `global.json` selects the .NET 11.0.100 SDK feature band and servicing
patches. `Directory.Build.props` enables the default .NET 11 analyzer set and
makes build warnings errors after a clean baseline review. Additional analyzer
rules and formatting sweeps remain separate changes; no extra analyzer packages
or coverage-percentage target are required.

`verify.ps1` builds the solution and runs both native xUnit executables, stopping
at the first failure and returning its exit code. Debug and Release are supported.
`tools/test-verify.ps1` checks command order, configuration, all failure stages,
and working directories with a recording fake in child processes.

The GitHub remote and .NET 11 SDK availability are established. The prepared
Windows workflow installs the selected SDK and invokes the script checks and
the same Release verification command. Hosted execution is still unverified
until the workflow is pushed. See [verification](../development/verification.md).
Console fixtures remain local to their test classes; sharing them is optional
future work when repeated composition justifies extraction.

## Verification and scope

The current baseline passes `verify.ps1` in Debug and Release with no build
warnings or errors. Warning-as-error enforcement and script failure propagation
are verified locally.
Both native xUnit runners pass: 354 core tests and 192 console tests.
This is regression protection, not proof of complete coverage or physical MIDI
behavior.

Implement each stage as a focused change and review its design before source
edits. Run the solution build and both test runners after code changes. Update
the relevant current-state Lode contracts with the resulting ownership and
boundaries. Hardware timing, disconnect recovery, and musical quality remain
separate validation concerns.

Related: [project practices](../practices.md), [project
structure](../architecture/project-structure.md), [console
commands](../development/console-commands.md), and [follow-up
review](follow-up-review.md).
