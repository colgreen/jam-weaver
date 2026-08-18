# Project practices

Repository-wide working, MIDI-safety, and verification instructions are in
[`AGENTS.md`](../AGENTS.md). This file records additional project conventions.

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
reader understand the type. Remarks may explain a significant guarantee,
limitation, rationale, or implementation approach, but should act as a bridge
into the code rather than repeat facts that are obvious from it. Keep both the
summary and any remarks accurate when the type's responsibility changes.

See [note lifecycle](midi/note-lifecycle.md) for the current output contract.
