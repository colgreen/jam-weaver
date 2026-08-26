# Project practices

Repository-wide working, MIDI-safety, and verification instructions are in
[`AGENTS.md`](../AGENTS.md). This file records additional project conventions.

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
