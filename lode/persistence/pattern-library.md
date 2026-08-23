# Pattern library

The pattern library stores accepted patterns as human-readable JSON. It keeps
musical content independent of MIDI ports, channels, patches, and other runtime
routing.

The console uses a `patterns` directory under its working directory. The library
owns only JSON files directly inside that directory and does not follow symbolic
links or scan subdirectories. It creates the directory on the first save.

## Files and identity

Each file contains one pattern. Its name combines a cleaned pattern name with
the first eight hexadecimal characters of the pattern ID. The full ID stored in
JSON is authoritative; the short filename suffix is only for readability and
collision avoidance.

Saving the same pattern ID under a new name safely writes the new file before
removing its old filename. Ambiguous duplicate IDs are reported instead of being
silently overwritten or deleted.

## Stored data

Format version 1 stores:

- Save time and pattern schema version.
- Pattern ID, name, mode, timing, and ordered steps.
- Each note's pitch, velocity, and gate, plus each step's trigger probability.
- Musical role and tonal context for melodic patterns.
- An optional generator recipe with generator ID, version, seed, parameters, and
  parent pattern ID.

Pitch and recipe values carry explicit type labels. Seeds are decimal strings so
all unsigned 64-bit values round-trip exactly. JSON is indented UTF-8 without a
byte-order mark.

Materialized steps are authoritative for playback. A recipe supports
reproduction and further mutation but never replaces the stored steps.

## Validation and compatibility

Reads are limited to 1 MiB. Loading rejects malformed JSON, duplicate
properties, unsupported format or pattern-schema versions, unknown type labels,
and values that violate the domain model. Unknown properties are tolerated so a
newer writer can add optional data without breaking an older reader.

Errors identify the file and failed operation without echoing its full content.
A malformed or duplicate entry is shown as invalid and does not prevent other
library entries from being listed.

## Save and load

Saves are serialized within the process. The complete JSON is prepared first,
then written to a unique temporary file in the library directory and atomically
moved into place. Cancellation or failure before replacement leaves the previous
file intact and cleans up the temporary file when possible.

Only the accepted pattern can be saved. An optional new name updates in-memory
metadata only after the file save succeeds.

The console treats the saved pattern name as the primary load identity. It
matches names case-insensitively and accepts names containing spaces. Displayed
one-based library positions are available as `load #<number>` to disambiguate
duplicate names; a bare number remains a convenience fallback. Generated
filenames and the library path are implementation details rather than the normal
save confirmation. Bare `load` lists current entries and the supported name and
number selection forms.

Load validates the selected file again, adds it to the recent-pattern list, and
introduces it as a candidate. It does not change the accepted pattern, MIDI
route, or transport. The loaded candidate becomes active immediately while
stopped or at the next bar while running.

The library deliberately has no delete command, migrations from nonexistent
older formats, route persistence, cloud synchronization, or automatic repair of
invalid hand-edited data.

Related: [pattern model](../sequencer/pattern-model.md) and [candidate
workflow](../performance/candidate-workflow.md).
