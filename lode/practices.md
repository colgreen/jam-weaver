# Project practices

Repository-wide working, MIDI-safety, and verification instructions are in
[`AGENTS.md`](../AGENTS.md). This file records additional project conventions.

## Verification and build policy

Use `pwsh -NoProfile -File ./verify.ps1` for the solution build and both native
xUnit runners. `-Configuration Release` matches CI. The script stops on failure
and preserves the failing exit code; it runs no hardware checks.

`global.json` selects the .NET 11.0.100 SDK feature band with servicing-patch
roll-forward. `Directory.Build.props` enables the default .NET 11 analyzer set
and makes build warnings errors. Review diagnostics before expanding this
policy; resolve warnings rather than broadly suppressing them. `.editorconfig`
sets formatting preferences without adding a formatting sweep or CI style gate.
See [verification](development/verification.md) for commands, script checks,
SDK selection, and the GitHub workflow.

## Type design and naming

Name classes, records, structs, and interfaces to describe their purpose as
concisely as the domain allows. Prefer a precise domain name over a longer name
that tries to encode implementation details or every use. A reader should
normally be able to infer the type's primary responsibility from its name.

Aim for types with one clear purpose. Keep their state and behavior focused on
that purpose, and split responsibilities when doing so produces a simpler,
more coherent design. This is guidance rather than a mechanical rule: a type
may combine responsibilities when there is a concrete reason and the resulting
design remains easier to understand and maintain.

Apply these standards to new and substantially changed code. Existing types are
not presumed to comply and do not need opportunistic renaming or restructuring.
Review them separately when there is a clear benefit and enough context to make
a sound design decision.

## Abstraction boundaries and ownership

Keep core contracts in domain terms. For example, a MIDI output exposes MIDI
operations rather than DryWetMIDI event objects, and rendering receives display
data rather than discovering devices. Keep native conversion in adapters and
composition in the console startup code. See [project
structure](architecture/project-structure.md) for the current boundaries.

An interface should describe behavior its implementations can honor without
callers downcasting, inspecting implementation state, or duplicating its policy.
Specify significant preconditions, failure behavior, ownership transfer, and
lifecycle ordering. Keep implementation-specific options local to the adapter
unless a concrete domain requirement needs them.

Keep mutable state private to its owner. Expose immutable values, snapshots, or
read-only views that cannot bypass the owner's validation or synchronization.
Choose snapshots when callers need a stable view of changing state; a read-only
interface alone does not make its backing collection immutable or thread-safe.
Keep transition decisions with the component that owns the state rather than
spreading them across callers.

Use constructor parameters or explicit operation arguments for dependencies.
Introduce interfaces where substitution, I/O isolation, or contract testing has
a concrete benefit; an interface for every class is not a design goal. Share an
invariant's implementation within its owner, while retaining validation at
boundaries that can receive untrusted or invalid values.

## Method structure and readability

Write methods around recognizable operations and keep orchestration distinct
from the detailed work it coordinates when that improves readability. Prefer
guard clauses and direct control flow over deep nesting or compressed chains
of side effects. Name important domain values and rules rather than leaving
unexplained literals in algorithms.

Extract a helper when it names a useful concept, isolates a responsibility, or
removes duplication of the same rule. Keep related logic together when splitting
it would force readers to jump between trivial helpers. Use comments to explain
rationale, constraints, and non-obvious ordering rather than paraphrase code.

Refactoring must preserve observable behavior, including ownership and failure
contracts. For deterministic generators, random-call order and fixed-seed output
are versioned behavior; follow [adding a
generator](development/adding-a-generator.md#version-change-checklist) when an
edit could change them. Run the relevant regression coverage after restructuring.

## Value types and validation

Struct constructors cannot prevent `default(T)`. When a default violates a
domain invariant, validate it where an aggregate, settings object, or public API
consumes it. Reuse the value type's internal validation rule and name the outer
argument in errors. Keep valid zero defaults usable; do not reject defaults as
a category or silently coerce invalid values into musical defaults.

Check externally supplied enums before switching on them. Reject invalid data
before changing state, sending MIDI, or consuming randomness. Persistence must
not encode a schema or document size its loader rejects. See [domain value
validation](sequencer/value-validation.md) for the current contracts.

## Global usings

Use each project's `Usings.cs` for namespaces that form its frequently used
vocabulary and would otherwise be repeated across many files. Keep uncommon
framework namespaces and specialized third-party dependencies local so a
file's unusual requirements remain visible. In particular, MIDI-library
adapters and generator-specific random-number dependencies should not become
global merely to shorten a few files.

Review global usings as the project changes. Add one only when repeated use is
established, and remove one when it no longer represents a broadly shared
dependency. Prefer a local using when the choice is borderline.

## Type documentation

Give classes, records, structs, and interfaces a concise XML `<summary>` when
the type name alone does not give a reader enough context to understand why the
type exists. This includes types whose names are broadly descriptive but leave
an important part of their responsibility unclear. Omit the summary when it
would merely restate an already clear name. Test classes normally do not need
one.

Write summaries in plain English. Prefer one direct sentence describing the
type's responsibility or observable promise rather than its implementation or
a list of its methods. Established project terms such as MIDI, pattern, and bar
are appropriate when they are clearer than substitutes. Avoid empty wording
such as "Represents", "Provides", or "A class that" when a direct verb works.

Use an XML `<remarks>` section only when additional context materially helps a
reader understand its behavior. Remarks may explain a significant guarantee,
limitation, rationale, or concise theory of operation, but should act as a
bridge into the code rather than repeat facts that are obvious from it.

Detailed theory of operation, subsystem interactions, design rationale, and
behavioral contracts belong in a focused Lode document. XML documentation
should give a developer enough local context to use or enter the type and, when
useful, point to that longer-lived explanation rather than duplicating it.

Documentation conventions also admit exceptions. Add or omit documentation
according to what makes the code clearest, and record a non-obvious design in
Lode when future work depends on understanding it. Keep summaries, remarks, and
linked Lode documents accurate when the type's responsibility changes.

See [note lifecycle](midi/note-lifecycle.md) for the current output contract.
