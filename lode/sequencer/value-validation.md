# Domain value validation

C# can produce `default(T)` for any struct without invoking its validating
constructor. Domain values therefore keep their existing value-type semantics,
while aggregates and APIs validate them before accepting them into stored state,
generation, routing, timing, or MIDI output.

An invalid value may exist as a standalone default struct; reading a numeric
property is not itself a validity guarantee. Validation belongs at the boundary
that consumes the value. Internal `Validate` methods share the relevant value
type's constructor rules and identify the consuming argument in failures.

## Default values

| Value | Meaning of default | Consumption policy |
| --- | --- | --- |
| `MidiValue` | MIDI data zero | Valid; note-on velocity in `PatternNote` must still be positive |
| `RootPitchClass` | C | Valid |
| `NormalizedAmount` | Amount zero | Valid |
| `TriggerProbability` | Never trigger | Valid |
| `RecipeValue` | Integer zero | Valid |
| `TonalContext` | C major pentatonic | Valid; its palette enum is checked when consumed |
| `MidiChannel` | Channel zero | Rejected by output, route, player channel selection, and zero-based conversion |
| `PatternId` | Empty GUID | Rejected by patterns and recipe parent identity |
| `PatternName` | Missing string | Rejected by patterns and generator settings |
| `PatternSchemaVersion` | Version zero | Rejected by patterns |
| `PatternTiming` | Zero pulses per step | Rejected by patterns, generator settings, and timelines |
| `NoteGate` | Zero duration | Rejected by `PatternNote` |
| `GeneratorLineage` | Missing ID/version | Rejected when explicitly supplied to a recipe |

Nullable absence remains distinct from an explicitly supplied default value.
For example, a null recipe parent is valid for fresh generation, but a nullable
parent containing `default(PatternId)` is invalid. Omitting lineage derives it
from the recipe's validated generator ID/version; explicit lineage must be valid.

## Aggregate and API boundaries

`Pattern` validates identity, name, positive schema version, timing, defined
mode/role/palette, and its existing mode/pitch relationships. `PatternNote`
validates gate as well as velocity; `PatternStep` retains duplicate-pitch and
null-note checks. Immutable pattern graphs can then be consumed without
rechecking their value objects on each playback pulse.

Generator settings validate name, timing where applicable, tonal palette, and
supported role along with their family-specific controls. Drum voice lists
reject null elements before accessing them. Pitch resolution rejects undefined
palettes and roles, including values created through `TonalContext` record-copy
syntax. Variation and groove entry points reject undefined enum controls rather
than selecting a fallback behavior or consuming random values first.

MIDI channel checks occur before sending messages or changing player routing.
Timeline configuration validates timing before replacing its current settings.
Transport rejects undefined clock-source and real-time-message enums before
mutating state or forwarding input. Internal tempo requires a finite value in
20-300 BPM; NaN and infinities cannot reach the clock loop through its owner.

These checks preserve valid-input algorithms and random-call order, so they do
not change generator versions or fixed-seed musical output. They do not expand
the supported scale palettes, role ranges, or musical interpretation of pitches.

## Persistence boundary

A positive schema version is a valid domain version value, but the JSON codec
only encodes and decodes the current supported pattern schema. It rejects
unsupported versions on write rather than producing a file it cannot load.
The codec also enforces its 1 MiB UTF-8 document limit on both encoding and
decoding, counting actual encoded bytes including the final newline.

Unknown generator algorithms remain valid recipe metadata when identity and
lineage are well-formed. Materialized steps stay authoritative; recipe
reconstruction support is separate from pattern persistence.

See [pattern model](pattern-model.md), [pattern
generation](../generation/pattern-generation.md), [pattern
library](../persistence/pattern-library.md), and [project practices](../practices.md).
