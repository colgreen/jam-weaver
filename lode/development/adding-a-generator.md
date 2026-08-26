# Adding a generator

A generator is a deterministic core component that converts validated,
generator-specific settings into an immutable materialized `Pattern` with a
complete `GeneratorRecipe`. Add one only when its musical behavior cannot be a
coherent version of an existing generator.

## 1. Define the contract

Create a settings record or class in `JamWeaver.Core.Generation` or a focused
subnamespace. Validate structural limits in its constructor so invalid settings
cannot reach the algorithm. Include the pattern name, seed, and every musical
input that affects output.

Implement the typed interface:

```csharp
public sealed class ExamplePatternGenerator
    : IPatternGenerator<ExampleGeneratorSettings>
{
    public const string GeneratorId = "melodic-example";
    public const int GeneratorVersion = 1;

    public Pattern Generate(ExampleGeneratorSettings settings)
    {
        // Validate cross-field constraints, generate steps, and create recipe.
    }
}
```

Use a stable, descriptive generator ID. Versions start at 1. Keep settings
strongly typed; do not introduce an untyped settings dictionary or common base
class merely to simplify console dispatch.

## 2. Generate deterministic material

Use `RandomDefaults.CreateRandomSource(settings.Seed)` for random choices.
Given the same ID, version, settings, seed, and parent material, output must be
musically identical. Do not use time, global random state, collection iteration
with unstable ordering, device state, or console state.

Build immutable `PatternStep` and `PatternNote` values. Validate MIDI data at
the established value boundaries. Melodic generators should store scale-degree
shapes with `MelodicPitch`; drum generators should use `DrumPitch`. Respect role
ranges and keep device-specific note names or mappings out of generic core
generation.

The generator must materialize every playback decision. Playback must never
need to rerun the generator.

## 3. Record and reconstruct the recipe

Create a `GeneratorRecipe` containing:

- The implementation's ID and version constants.
- The generation seed.
- A parent pattern ID only when the operation derives from a parent.
- Every resolved input required to explain and reproduce the result.
- Useful structural metadata when it supports mutation or diagnostics.

Use stable kebab-case parameter keys and `RecipeValue` factories. Recipe values
are persistence contracts; changing a key or meaning requires version review.

Add a matching method to `GeneratorRecipeReconstruction`. It must require the
exact ID and version, reject missing and unknown keys, validate types and
cross-field relationships, and return settings that reproduce the pattern.
Materialized saved steps remain usable even when reconstruction is unsupported.

## 4. Compose the generator

For a performer-selectable generator, update the console explicitly:

1. Add its `GeneratorMode` value and construct its implementation at startup.
2. Add its command name, help text, and relevant controls.
3. Add its state to `GenerationControls` and extend `CandidateGenerator` to
   build typed settings from the current candidate context and performer
   controls.
4. Decide which controls it supports and reject incompatible ones clearly.
5. Preserve the current tonal context and role where the generator supports
   them.

Do not add a generic registry unless another concrete consumer needs runtime
discovery. The interface provides a testable core contract; the explicit
`CandidateGenerator` switch documents console-specific composition.

If a generator is intentionally experimental or core-only, document that and
omit console wiring rather than exposing an incomplete command path.

## 5. Test the contract

At minimum, add automated coverage for:

- Implementation of `IPatternGenerator<TSettings>`.
- Identical musical output for identical settings and seed.
- Representative different seeds producing different material.
- A fixed-seed signature or snapshot protecting the version contract.
- MIDI, role, timing, length, density, and other range invariants.
- Recipe identity, version, and complete parameter contents.
- Reconstruction producing musically equivalent output.
- Rejection of missing, unknown, mistyped, inconsistent, and unsupported recipe
  data.
- Structural musical invariants promised by the generator.

Automated tests establish repeatability and invariants, not musical quality.
Report physical-device audition separately and update the relevant hardware
validation document only after it occurs.

## 6. Update project knowledge

Add a focused document under `lode/generation/` when the algorithm has musical
contracts or rationale beyond the shared generation document. Update:

- [Pattern generation](../generation/pattern-generation.md) with its current
  availability and shared constraints.
- [Lode map](../lode-map.md) with any new focused document.
- [Project summary](../summary.md) if current capabilities or limitations change.
- Persistence, performance, or hardware documents when their contracts change.

Describe the resulting current behavior, including generator ID and version.
Do not record a changelog or transient implementation history.

## Version-change checklist

For a change to an existing generator, determine whether fixed-seed output can
change. Algorithm edits, reordered random calls, and random-library changes
normally require a version increment. Then update recipe reconstruction,
snapshots, focused generation documentation, and any saved-recipe compatibility
policy together.
