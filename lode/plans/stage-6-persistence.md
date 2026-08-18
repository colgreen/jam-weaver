# Stage 6: versioned pattern persistence

> Status: implemented and covered by the xUnit suite.

Stage 6 adds a durable, human-readable library for accepted patterns. It stores
canonical materialized steps and optional generator ancestry, recalls saved
patterns as audition candidates, and keeps filesystem work away from MIDI timing
callbacks. Routing and device configuration remain performance state.

## Source structure

```text
src/JamWeaver.Core/Persistence/
  PatternJsonCodec.cs
  PatternLibrary.cs
  PatternLibraryEntry.cs
  PatternPersistenceException.cs
```

DTOs are private to `PatternJsonCodec`; persistence shapes do not become domain
APIs. `System.Text.Json` from .NET 8 is sufficient, so Stage 6 adds no package.

## Library location and ownership

The console uses `<working-directory>/patterns` and prints its resolved absolute
path in `library`. `PatternLibrary` accepts an explicit root directory, making a
future configuration setting and isolated tests straightforward.

The library owns only `*.json` files directly inside its root. It does not
recurse, follow a path supplied by a command, or persist MIDI port/channel data.
The directory is created on the first save, not merely by listing.

## File identity and names

One JSON file contains one pattern. Its filename is:

```text
<sanitized-name>--<first-8-lowercase-id-hex>.json
```

Sanitization lowercases invariantly, maps each run of non-ASCII-alphanumeric
characters to one hyphen, trims hyphens, and limits the name component to 48
characters. An empty result becomes `pattern`. The ID suffix prevents same-name
collisions.

The library searches existing files by the full pattern ID stored in JSON, not
only by the short filename suffix. Saving the same ID under a new name writes the
new target and removes the prior filename only after the new file is safely in
place. This cleanup is a tightly scoped, recoverable consequence of rename; an
unexpected duplicate is reported rather than deleting ambiguous files.

## JSON envelope

The root object contains:

- `formatVersion`: integer, initially 1.
- `savedUtc`: ISO-8601 UTC timestamp for display/diagnostics, not identity.
- `pattern`: the complete snapshot.

The pattern object contains:

- `schemaVersion`, `id`, `name`, `mode`, and `pulsesPerStep`.
- Ordered `steps`, each with `probability` and ordered `notes`.
- Each note's `velocity`, `gate`, and discriminated `pitch`.
- Nullable melodic `role` and `tonalContext`.
- Nullable `recipe`.

Pitch representations are explicit:

```json
{ "kind": "melodic", "scaleDegree": 0, "octaveOffset": 0, "chromaticOffset": 0 }
{ "kind": "drum", "noteNumber": 36 }
```

Recipe contains generator ID/version, seed as an invariant decimal string,
nullable parent pattern ID, and an ordinally keyed parameter object. Every
parameter is discriminated so integer and floating-point values round-trip:

```json
"density": { "kind": "number", "value": 0.4 }
"hits": { "kind": "integer", "value": 6 }
```

Enums serialize as stable lower camel-case strings defined by the codec rather
than runtime enum-name policy. JSON is indented, UTF-8 without BOM, and ends with
a newline. Property order is fixed for readable diffs but is not a load
requirement.

## Codec contract and validation

`PatternJsonCodec` exposes encode and decode operations over UTF-8 data. Decode:

1. Rejects empty input and input larger than 1 MiB.
2. Requires a JSON object and all fields needed by the selected format version.
3. Rejects a `formatVersion` other than 1 with a compatibility-specific error.
4. Rejects a pattern `schemaVersion` other than the current version. Future
   migrations can dispatch here without weakening current validation.
5. Rejects duplicate JSON properties and duplicate recipe parameter names.
6. Rejects unknown enum strings and pitch/recipe discriminators.
7. Constructs normal domain value types so all existing ranges and melodic/drum
   consistency rules remain authoritative.

Unknown properties are ignored at every object level for additive forward
compatibility. Required known properties cannot be null. JSON numeric parsing
must remain finite and culture invariant. Exceptions are wrapped in
`PatternPersistenceException` with filename/context but no full JSON content.

## Atomic save

`SaveAsync` accepts a pattern and cancellation token:

1. Resolve and verify the library root and final target remain within the
   configured root.
2. Create the root if absent.
3. Encode fully before touching the destination.
4. Write a uniquely named temporary file in the same directory using create-new
   semantics, flush it, then atomically move it over the exact target.
5. Remove the temporary file on cancellation/failure when possible.
6. After success, remove a single prior filename for the same full pattern ID if
   the pattern was renamed.

Concurrent saves in one process are serialized. Cancellation before replacement
leaves the old file intact. The returned entry reflects the final path and saved
metadata.

## Listing and recall

`ListAsync` returns immutable `PatternLibraryEntry` values sorted by pattern name
ordinal-ignore-case, then full ID. Each valid entry contains display metadata
without exposing mutable DTOs: name, ID, mode, optional role/context and seed,
saved timestamp, and filename.

Malformed files produce entries marked invalid with a concise error instead of
failing the whole listing. Symlinks/reparse points and oversized files are
reported invalid and never followed. Duplicate full IDs are reported invalid so
numbered recall cannot silently choose one.

`LoadAsync(entry)` reopens the selected filename and validates it again; list
metadata is never trusted as pattern content. The console rebuilds the same
sorted snapshot for each `recall <number>`, validates the one-based index, and
refuses invalid entries. A concurrently renamed/deleted file yields a clear
retryable error.

Recall calls `CandidateSession.SetCandidate`. It never changes Accepted, undo
history, player channel, output port, or transport. Existing Stage 5 timing makes
the result immediate while stopped and bar-quantized while running.

## Save and rename semantics

`save [name]` requires an Accepted pattern. With no name it saves Accepted as-is.
With a name, it validates `PatternName`, saves `Accepted.Rename(name)`, and only
after successful persistence updates the session's in-memory metadata.

`CandidateSession.RenameAccepted` updates Accepted and any Candidate,
PreviousAccepted, current-player, or pending-player reference with the same ID.
`PatternPlayer.ReplaceMetadata` accepts only a same-ID pattern and replaces
matching references immediately because renaming cannot affect MIDI output. A
failed save leaves all session names unchanged.

## Console commands

```text
library
save [name]
recall <number>
```

Names may contain spaces: `save` uses the remainder of the command line after
the command rather than whitespace-token parsing. The library menu shows:

- One-based number and name.
- Melodic/drums.
- Friendly key and role for melodic patterns.
- Seed when a recipe exists.
- A concise invalid marker for unreadable entries.

Successful save prints the filename and full library directory. Successful
recall reports `audible` or `pending for next bar`. Persistence commands execute
on the console thread and never from transport callbacks.

## xUnit verification

The codec tests cover:

- Exact round-trip of melodic and multi-voice drum patterns.
- Empty steps, all pitch fields, probability/gate/velocity, role/context, IDs,
  schema version, and every recipe value kind including `ulong.MaxValue` seed.
- Stable version-1 property names, decimal-string maximum seed, and newline.
- Unknown-property tolerance.
- Duplicate properties, unknown format/schema versions, mode and pitch
  discriminators, invalid gate data, malformed/empty JSON, and the size cap.

Library tests use a unique temporary directory and cover:

- Directory creation, filename sanitization, same-name distinct IDs, and
  deterministic sorting.
- Atomic overwrite of the same ID and rename cleanup.
- Pre-cancellation preserving the prior destination without temporary files.
- A malformed entry not blocking valid entries and duplicate IDs becoming
  invalid.
- Save-time rename updating only matching session/player metadata references.

Tests remove only their resolved unique temporary directory. They do not touch
the real `patterns` directory or require MIDI hardware.

## Explicitly deferred

- Delete, overwrite-by-name, tags, favorites, search, and subdirectories.
- Configurable/default user-profile library locations and cloud sync.
- Import/export bundles and migrations from format versions that do not exist.
- Persisting performance routes, patches, device maps, or UI settings.
- Automatic recovery from hand-edited invalid musical data.

## Resolved decisions

- The initial library is a visible `patterns` directory under the working
  directory, with one atomically written JSON file per pattern.
- Only Accepted is saved; recall creates a Candidate and requires audition.
- Files use explicit format/schema versions, discriminated pitches and recipe
  values, exact decimal-string seeds, and tolerate unknown properties.
- The numbered menu has no delete command in Stage 6.

Related: [pattern model](../sequencer/pattern-model.md),
[candidate workflow](../performance/candidate-workflow.md), and
[initial sequencer plan](initial-sequencer.md).
