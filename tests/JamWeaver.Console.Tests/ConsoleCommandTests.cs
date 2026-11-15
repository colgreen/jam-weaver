using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Transport;
using Redzen.Random;

namespace JamWeaver.Console.Tests;

public sealed class ConsoleCommandTests
{
    [Theory]
    [InlineData("  ")]
    [InlineData("\t ")]
    public async Task Indented_bare_save_and_load_preserve_the_accepted_name_and_list_the_library(string indent)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;

        await fixture.RunAsync($"{indent}save  \n{indent}load  \nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Same(accepted, fixture.Candidates.Accepted);
        Assert.Same(accepted, fixture.Candidates.Candidate);
        var entry = Assert.Single(await fixture.Library.ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(accepted.Name.Value, entry.Name);
        Assert.Contains($"Saved '{accepted.Name}'.", fixture.Transcript);
        Assert.Contains("Load with: load <name> or load #<number>", fixture.Transcript);
    }

    [Theory]
    [InlineData("  save Review Indent", "  load Review Indent", "Review Indent")]
    [InlineData("\tSaVe\tReview  Indent  ", "\tLoAd\tReview  Indent  ", "Review  Indent")]
    [InlineData("  save Review Indent", "  load #1", "Review Indent")]
    [InlineData("  save Review Indent", "  load 1", "Review Indent")]
    public async Task Indented_save_and_load_round_trip_names_and_materialized_patterns(string save, string load, string name)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;

        await fixture.RunAsync($"{save}\nnew 456\n{load}\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(accepted.Id, fixture.Candidates.Accepted!.Id);
        Assert.Equal(name, fixture.Candidates.Accepted.Name.Value);
        Assert.Equal(accepted.Id, fixture.Candidates.Candidate!.Id);
        Assert.Equal(name, fixture.Candidates.Candidate.Name.Value);
        var loaded = fixture.Candidates.Candidate;
        Assert.Equal(accepted.Steps.Length, loaded.Steps.Length);
        for (var index = 0; index < accepted.Steps.Length; index++)
        {
            Assert.Equal(accepted.Steps[index].Probability, loaded.Steps[index].Probability);
            Assert.Equal(accepted.Steps[index].Notes, loaded.Steps[index].Notes);
        }
        Assert.Equal(accepted.Recipe!.Seed, fixture.Candidates.Candidate.Recipe!.Seed);
        Assert.Same(fixture.Candidates.Candidate, fixture.Player.CurrentPattern);
        Assert.Contains($"Loaded '{name}' as a candidate", fixture.Transcript);
        Assert.Equal(name, Assert.Single(await fixture.Library.ListAsync(TestContext.Current.CancellationToken)).Name);
    }

    public static TheoryData<string> ExtraArgumentCommands => new()
    {
        "setup extra", "out 0 extra", "in 0 extra", "source internal extra", "bpm 150 extra",
        "start extra", "continue extra", "stop extra", "go extra", "new 999 extra", "compare 999 extra",
        "vary all .5 seed 999 extra", "back extra", "forward extra", "generator motif extra", "groove offbeat extra",
        "shape riff extra", "similarity close extra", "length 1 extra", "activity busy extra", "rhythm steady extra",
        "movement high extra", "variation high extra", "turnaround strong extra", "settings extra", "keep extra",
        "revert extra", "key up extra", "palette extra", "role high extra", "ch 2 extra", "channel 2 extra",
        "play extra", "mute extra", "pattern extra", "library extra", "note 1 60 100 0 extra",
        "on 1 60 100 extra", "off 1 60 0 extra", "cc 1 7 80 extra", "pc 1 10 extra",
        "panic extra", "status extra", "help new extra", "quit extra", "exit extra"
    };

    [Theory]
    [MemberData(nameof(ExtraArgumentCommands))]
    public async Task Extra_arguments_are_rejected_before_command_effects_and_the_loop_continues(string command)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted;
        var controls = fixture.ControlSnapshot();
        var transport = fixture.Transport.Snapshot;

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains("Error: Unexpected", fixture.Transcript);
        Assert.Contains("Output=command-test", fixture.Transcript);
        Assert.Same(accepted, fixture.Candidates.Accepted);
        Assert.Same(accepted, fixture.Candidates.Candidate);
        Assert.Same(accepted, fixture.Player.CurrentPattern);
        Assert.Equal(1, fixture.History.Count);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Equal(transport, fixture.Transport.Snapshot);
        Assert.False(fixture.Player.IsEnabled);
        Assert.Equal(1, fixture.Player.Channel.Number);
        Assert.Equal(120, fixture.Clock.Bpm);
        Assert.Equal(0, fixture.PortOpenAttempts);
        Assert.Empty(fixture.Port.Messages);
        Assert.False(Directory.Exists(fixture.Library.RootDirectory));
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Theory]
    [InlineData("note 1 60")]
    [InlineData("on 1 60")]
    [InlineData("off 1")]
    [InlineData("cc 1 7")]
    [InlineData("pc 1")]
    [InlineData("key")]
    [InlineData("role")]
    public async Task Missing_arguments_are_reported_without_effects_and_the_loop_continues(string command)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted;

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains("Error: Missing argument", fixture.Transcript);
        Assert.Contains("Output=command-test", fixture.Transcript);
        Assert.Same(accepted, fixture.Candidates.Candidate);
        Assert.Empty(fixture.Port.Messages);
    }

    [Fact]
    public async Task Whitespace_and_existing_optional_arguments_remain_usable()
    {
        await using var fixture = new Session();

        await fixture.RunAsync("  \n\t\n\tBPM\t150\nsource\nbpm\ngenerator\nshape\nch\nchannel\nhelp new\n"
            + "vary seed 1\nvary rhythm .5 seed 2\non\t1\t60\t100\noff 1 60\nnote 1 61 100 0\npc 1 10\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(150, fixture.Clock.Bpm);
        Assert.Equal(["On", "Off", "On", "Off", "Program"], fixture.Port.Messages);
        Assert.Equal(3, fixture.History.Count);
    }

    [Theory]
    [InlineData("source unknown", "Source must be internal or external.")]
    [InlineData("generator unknown", "Generator must be euclidean, turnaround, motif, phrase, or groove.")]
    [InlineData("groove unknown", "Unknown groove category.")]
    [InlineData("shape unknown", "Unknown motif shape.")]
    [InlineData("similarity 99", "Invalid GrooveSimilarity value.")]
    [InlineData("length 3", "Length must be 1, 2, or 4 bars.")]
    [InlineData("activity invalid", "Invalid PhraseActivity value.")]
    [InlineData("rhythm -1", "Invalid PhraseRhythm value.")]
    [InlineData("movement 99", "Invalid PhraseLevel value.")]
    [InlineData("variation 99", "Invalid PhraseLevel value.")]
    [InlineData("turnaround 99", "Invalid PhraseTurnaround value.")]
    [InlineData("key unknown", "Key direction must be up or down.")]
    [InlineData("role unknown", "Role must be bass, middle, or high.")]
    [InlineData("ch 0", "Error:")]
    [InlineData("ch 17", "Error:")]
    [InlineData("on 1 -1 100", "Error:")]
    [InlineData("on 1 60 128", "Error:")]
    [InlineData("bpm NaN", "BPM must be finite and 20-300.")]
    public async Task Invalid_argument_values_preserve_state_and_leave_the_command_loop_available(string command, string error)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        var controls = fixture.ControlSnapshot();
        var transport = fixture.Transport.Snapshot;

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains(error, fixture.Transcript);
        Assert.Single(fixture.Transcript.Split('\n'), line => line.Contains("Error:", StringComparison.Ordinal));
        Assert.Contains("Output=command-test", fixture.Transcript);
        fixture.AssertStoppedSelection(accepted, accepted, 1, 1);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Equal(transport, fixture.Transport.Snapshot);
        Assert.Equal(1, fixture.Player.Channel.Number);
        Assert.Equal(120, fixture.Clock.Bpm);
        Assert.Empty(fixture.Port.Messages);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Fact]
    public async Task Named_choices_aliases_and_numeric_boundaries_remain_usable()
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;

        await fixture.RunAsync("generator GrOoVe\ngroove long-short\nsimilarity close\nlength 2\nactivity busy\n"
            + "rhythm steady\nmovement low\nvariation high\nturnaround none\nshape root-fifth\n"
            + "role mid\nkey DoWn\nch 16\nbpm +150\non 16 0 127\noff 16 0 0\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(GeneratorMode.Groove, fixture.Controls.Mode);
        Assert.Equal(GrooveSelection.LongShort, fixture.Controls.Groove);
        Assert.Equal(GrooveSimilarity.Close, fixture.Controls.Similarity);
        Assert.Equal(PhraseLength.TwoBars, fixture.Controls.PhraseLength);
        Assert.Equal(PhraseActivity.Busy, fixture.Controls.Activity);
        Assert.Equal(PhraseRhythm.Steady, fixture.Controls.Rhythm);
        Assert.Equal(PhraseLevel.Low, fixture.Controls.Movement);
        Assert.Equal(PhraseLevel.High, fixture.Controls.Variation);
        Assert.Equal(PhraseTurnaround.None, fixture.Controls.Turnaround);
        Assert.Equal(MotifShape.RootFifth, fixture.Controls.MotifShape);
        var candidate = fixture.Candidates.Candidate!;
        Assert.Equal(MusicalRole.Middle, candidate.Role);
        Assert.Equal(8, candidate.TonalContext!.Value.Root.Value);
        fixture.AssertStoppedSelection(candidate, accepted, 3, 3);
        Assert.Equal(16, fixture.Player.Channel.Number);
        Assert.Equal(150, fixture.Clock.Bpm);
        Assert.Equal(["On", "Off"], fixture.Port.Messages);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Theory]
    [InlineData("vary", null, .3, null)]
    [InlineData("vary .7", null, .7, null)]
    [InlineData("vary 0.0 seed 456", null, 0, 456UL)]
    [InlineData("vary 1.0 seed 456", null, 1, 456UL)]
    [InlineData("vary seed 0", null, .3, 0UL)]
    [InlineData("vary notes", VariationTarget.Notes, .3, null)]
    [InlineData("  VaRy\tRhYtHm\t.5\tSeEd\t123  ", VariationTarget.Rhythm, .5, 123UL)]
    [InlineData("vary expression .5", VariationTarget.Expression, .5, null)]
    [InlineData("vary turnaround seed 456", VariationTarget.Turnaround, .3, 456UL)]
    [InlineData("vary all .7 seed 18446744073709551615", VariationTarget.All, .7, ulong.MaxValue)]
    [InlineData("vary 0", VariationTarget.Rhythm, .3, null)]
    [InlineData("vary 1", VariationTarget.Notes, .3, null)]
    public async Task Variation_grammar_preserves_target_strength_seed_and_materialized_output(
        string command, VariationTarget? target, double strength, ulong? explicitSeed)
    {
        await using var fixture = new Session();
        var parent = fixture.Candidates.Candidate!;
        var controls = fixture.ControlSnapshot();
        var seeds = RandomDefaults.CreateRandomSource(123);
        var seed = explicitSeed ?? seeds.NextULong();
        var expected = new EuclideanMotifGenerator().Vary(parent,
            new GeneratorVariationSettings(target, new NormalizedAmount(strength), seed));

        await fixture.RunAsync($"{command}\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        var candidate = fixture.Candidates.Candidate!;
        AssertEquivalentGeneratedPattern(expected, candidate);
        Assert.Equal(parent.Id, candidate.Recipe!.ParentPatternId);
        Assert.Equal(parent.Recipe!.Lineage, candidate.Recipe.Lineage);
        fixture.AssertStoppedSelection(candidate, parent, 2, 2);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Equal(seeds.NextULong(), fixture.Seeds.NextULong());
        Assert.Empty(fixture.Port.Messages);
        Assert.Contains(target is { } selected
            ? $"Varied {selected.ToString().ToLowerInvariant()} with seed {seed} at strength {strength:0.##}."
            : $"Varied candidate with seed {seed} at strength {strength:0.##}.", fixture.Transcript);
    }

    [Theory]
    [InlineData("vary seed", "Missing argument.", false)]
    [InlineData("vary .5 seed", "Missing argument.", false)]
    [InlineData("vary rhythm .5 seed", "Missing argument.", false)]
    [InlineData("vary .5 123", "Expected 'seed <number>' after variation strength.", false)]
    [InlineData("vary .5 seed 123 extra", "Unexpected vary argument.", false)]
    [InlineData("vary seed 123 extra", "Unexpected vary argument.", false)]
    [InlineData("vary unknown", "Error:", false)]
    [InlineData("vary .5 seed invalid", "Error:", false)]
    [InlineData("vary .5 seed -1", "Error:", false)]
    [InlineData("vary .5 seed 18446744073709551616", "Error:", false)]
    [InlineData("vary -1", "value", true)]
    [InlineData("vary 1.1", "value", true)]
    [InlineData("vary NaN", "value", true)]
    [InlineData("vary Infinity", "value", true)]
    [InlineData("vary 1.1 seed 456", "value", false)]
    public async Task Invalid_variation_preserves_state_and_the_existing_seed_consumption_order(
        string command, string error, bool consumesSeed)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        var controls = fixture.ControlSnapshot();
        var transport = fixture.Transport.Snapshot;

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains(error, fixture.Transcript);
        Assert.Single(fixture.Transcript.Split('\n'), line => line.Contains("Error:", StringComparison.Ordinal));
        Assert.Contains("Output=command-test", fixture.Transcript);
        fixture.AssertStoppedSelection(accepted, accepted, 1, 1);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Equal(transport, fixture.Transport.Snapshot);
        Assert.Empty(fixture.Port.Messages);
        var seeds = RandomDefaults.CreateRandomSource(123);
        if (consumesSeed) seeds.NextULong();
        Assert.Equal(seeds.NextULong(), fixture.Seeds.NextULong());
    }

    [Theory]
    [InlineData("vary")]
    [InlineData("vary .7")]
    [InlineData("vary .7 seed -1")]
    public async Task Variation_requires_a_candidate_before_parsing_optional_arguments(string command)
    {
        await using var fixture = new Session(createInitialCandidate: false);

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains("Error: Create a candidate with 'new' first.", fixture.Transcript);
        Assert.Contains("Output=command-test", fixture.Transcript);
        Assert.Null(fixture.Candidates.Candidate);
        Assert.Null(fixture.Candidates.Accepted);
        Assert.Null(fixture.Player.CurrentPattern);
        Assert.Null(fixture.Player.PendingPattern);
        Assert.Equal(0, fixture.History.Count);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
        Assert.Empty(fixture.Port.Messages);
    }

    [Theory]
    [InlineData("generator help")]
    [InlineData("generator ?")]
    [InlineData("help generator")]
    public async Task Generator_help_explains_choices_without_changing_the_session(string command)
    {
        await using var fixture = new Session();
        var candidate = fixture.Candidates.Candidate;
        var controls = fixture.ControlSnapshot();

        await fixture.RunAsync($"{command}\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Contains("1-bar loop", fixture.Transcript);
        Assert.Contains("bass only", fixture.Transcript);
        Assert.Contains("inactive", fixture.Transcript);
        Assert.DoesNotContain("euclidean-phrase", fixture.Transcript);
        Assert.DoesNotContain("euclidean2", fixture.Transcript);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Same(candidate, fixture.Candidates.Candidate);
        Assert.Equal(1, fixture.History.Count);
        Assert.Equal(0, fixture.PortOpenAttempts);
        Assert.Empty(fixture.Port.Messages);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Theory]
    [InlineData("euclidean-phrase")]
    [InlineData("EuClIdEaN2")]
    public async Task Retired_generator_names_are_rejected_without_changing_the_session(string choice)
    {
        await using var fixture = new Session();
        fixture.Controls.Mode = GeneratorMode.Motif;
        var candidate = fixture.Candidates.Candidate;
        var controls = fixture.ControlSnapshot();

        await fixture.RunAsync($"generator {choice}\nquit\n");

        Assert.Contains("Euclidean phrase is no longer selectable", fixture.Transcript);
        Assert.Contains("generator phrase", fixture.Transcript);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Same(candidate, fixture.Candidates.Candidate);
        Assert.Same(candidate, fixture.Player.CurrentPattern);
        Assert.Equal(1, fixture.History.Count);
        Assert.Equal(0, fixture.PortOpenAttempts);
        Assert.Empty(fixture.Port.Messages);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Fact]
    public async Task Saved_euclidean_phrase_patterns_still_load_and_support_repeated_variation()
    {
        await using var fixture = new Session();
        var legacy = new EuclideanPhraseGenerator().Generate(new EuclideanGeneratorSettings(
            new PatternName("Legacy phrase"), 64, PatternTiming.SixteenthNotes,
            CandidateGenerator.DefaultTonalContext(), MusicalRole.Bass,
            new NormalizedAmount(.4), new NormalizedAmount(.35), new NormalizedAmount(.65),
            new NormalizedAmount(.8), new NormalizedAmount(.15), 123));
        await fixture.Library.SaveAsync(legacy, TestContext.Current.CancellationToken);

        await fixture.RunAsync("load #1\nkeep\nvary seed 456\nvary notes .3 seed 789\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        var accepted = fixture.Candidates.Accepted!;
        Assert.Equal(legacy.Id, accepted.Id);
        Assert.Equal(legacy.Timing, accepted.Timing);
        Assert.Equal(legacy.Steps.Length, accepted.Steps.Length);
        for (var index = 0; index < legacy.Steps.Length; index++)
        {
            Assert.Equal(legacy.Steps[index].Notes, accepted.Steps[index].Notes);
            Assert.Equal(legacy.Steps[index].Probability, accepted.Steps[index].Probability);
        }
        Assert.Equal(4, fixture.History.Count);
        var varied = fixture.Candidates.Candidate!;
        Assert.NotEqual(legacy.Id, varied.Id);
        Assert.Equal(legacy.Recipe!.Lineage, varied.Recipe!.Lineage);
        Assert.Equal(789UL, varied.Recipe.Seed);
        Assert.Same(varied, fixture.Player.CurrentPattern);
        Assert.Empty(fixture.Port.Messages);
    }

    [Fact]
    public async Task Generator_listing_and_advanced_help_offer_only_current_choices()
    {
        await using var fixture = new Session();

        await fixture.RunAsync("generator\nhelp advanced\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Contains("Choices: euclidean, turnaround, motif, phrase, groove", fixture.Transcript);
        Assert.DoesNotContain("euclidean-phrase", fixture.Transcript);
        Assert.DoesNotContain("euclidean2", fixture.Transcript);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compare_prepares_matched_four_bar_candidates_and_toggles_without_adding_history(bool explicitSeed)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        await fixture.RunAsync("key up\npalette\ngenerator motif\nlength 1\nactivity busy\nrhythm broken\n"
            + "movement high\nvariation low\nturnaround strong\ngroove offbeat\nsimilarity contrast\nquit\n");
        var context = fixture.Candidates.Candidate!.TonalContext!.Value;
        var controls = fixture.ControlSnapshot();
        var seeds = RandomDefaults.CreateRandomSource(123);
        var seed = explicitSeed ? 456UL : seeds.NextULong();
        var name = new PatternName($"Compare {seed}");
        var expectedPhrase = new StructuredPhraseGenerator().Generate(new PhraseGeneratorSettings(name,
            PhraseLength.FourBars, context, MusicalRole.Bass, PhraseActivity.Busy, PhraseRhythm.Broken,
            PhraseLevel.High, PhraseLevel.Low, PhraseTurnaround.Strong, seed));
        var expectedGroove = new GrooveVocabularyGenerator().Generate(new GrooveGeneratorSettings(name,
            context, MusicalRole.Bass, GrooveSelection.Offbeat, GrooveSimilarity.Contrast,
            PhraseActivity.Busy, PhraseLevel.High, PhraseLevel.Low, PhraseTurnaround.Strong, seed));
        Pattern? phrase = null;
        Pattern? groove = null;

        await fixture.RunAsync(new ScriptedReader(
            (explicitSeed ? "compare 456" : "compare", () =>
            {
                groove = fixture.Candidates.Candidate!;
                AssertEquivalentGeneratedPattern(expectedGroove, groove);
                fixture.AssertStoppedSelection(groove, accepted, 5, 5);
                Assert.Contains($"Prepared matched comparison with seed {seed}.", fixture.Transcript);
                Assert.Contains("Comparison: groove", fixture.Transcript);
            }),
            ("compare", () =>
            {
                phrase = fixture.Candidates.Candidate!;
                AssertEquivalentGeneratedPattern(expectedPhrase, phrase);
                Assert.NotEqual(groove!.Id, phrase.Id);
                fixture.AssertStoppedSelection(phrase, accepted, 5, 5);
                Assert.Contains("Comparison: phrase", fixture.Transcript);
            }),
            ("compare", () => fixture.AssertStoppedSelection(groove!, accepted, 5, 5)),
            ("compare", () => fixture.AssertStoppedSelection(phrase!, accepted, 5, 5))));

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Equal(seeds.NextULong(), fixture.Seeds.NextULong());
        Assert.Empty(fixture.Port.Messages);
        Assert.Equal(0, fixture.PortOpenAttempts);
    }

    [Fact]
    public async Task First_comparison_without_a_candidate_establishes_groove_as_the_safe_point()
    {
        await using var fixture = new Session(createInitialCandidate: false);
        Pattern? groove = null;
        Pattern? phrase = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () =>
            {
                groove = fixture.Candidates.Candidate!;
                Assert.Equal(GrooveVocabularyGenerator.GeneratorId, groove.Recipe!.GeneratorId);
                Assert.Equal(123UL, groove.Recipe.Seed);
                Assert.Equal(CandidateGenerator.DefaultTonalContext(), groove.TonalContext);
                Assert.Equal(MusicalRole.Bass, groove.Role);
                fixture.AssertStoppedSelection(groove, groove, 2, 2);
            }),
            ("back", () =>
            {
                phrase = fixture.Candidates.Candidate!;
                Assert.Equal(StructuredPhraseGenerator.GeneratorId, phrase.Recipe!.GeneratorId);
                Assert.Equal(123UL, phrase.Recipe.Seed);
                Assert.Equal(groove!.TonalContext, phrase.TonalContext);
                Assert.Equal(MusicalRole.Bass, phrase.Role);
                fixture.AssertStoppedSelection(phrase, groove, 2, 1);
            }),
            ("revert", () => fixture.AssertStoppedSelection(groove!, groove!, 2, 1)),
            ("compare", () => fixture.AssertStoppedSelection(phrase!, groove!, 2, 1))));

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
        Assert.Empty(fixture.Port.Messages);
    }

    [Fact]
    public async Task Prepared_comparison_reuses_its_patterns_after_controls_and_tonal_context_change()
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? groove = null;
        Pattern? phrase = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () => groove = fixture.Candidates.Candidate!),
            ("compare", () => phrase = fixture.Candidates.Candidate!),
            ("activity busy", () => Assert.Equal(PhraseActivity.Busy, fixture.Controls.Activity)),
            ("key up", () => Assert.NotEqual(phrase!.TonalContext, fixture.Candidates.Candidate!.TonalContext)),
            ("compare", () => fixture.AssertStoppedSelection(groove!, accepted, 4, 4)),
            ("compare", () => fixture.AssertStoppedSelection(phrase!, accepted, 4, 4)),
            ("compare 456", () =>
            {
                var replacement = fixture.Candidates.Candidate!;
                Assert.NotEqual(groove!.Id, replacement.Id);
                Assert.Equal(456UL, replacement.Recipe!.Seed);
                Assert.Equal(RecipeValue.FromText("Busy"), replacement.Recipe.Parameters["activity"]);
                Assert.Equal(phrase!.TonalContext, replacement.TonalContext);
                fixture.AssertStoppedSelection(replacement, accepted, 6, 6);
            })));

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Fact]
    public async Task Browsing_after_comparison_toggling_uses_phrase_then_groove_history_order()
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? phrase = null;
        Pattern? groove = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () => groove = fixture.Candidates.Candidate!),
            ("compare", () =>
            {
                phrase = fixture.Candidates.Candidate!;
                fixture.AssertStoppedSelection(phrase, accepted, 3, 3);
            }),
            // Toggling selects phrase but leaves the history cursor on groove.
            ("back", () => fixture.AssertStoppedSelection(phrase!, accepted, 3, 2)),
            ("back", () => fixture.AssertStoppedSelection(accepted, accepted, 3, 1)),
            ("forward", () => fixture.AssertStoppedSelection(phrase!, accepted, 3, 2)),
            ("forward", () => fixture.AssertStoppedSelection(groove!, accepted, 3, 3))));

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Contains("Candidate history 1/3.", fixture.Transcript);
        Assert.Contains("Candidate history 2/3.", fixture.Transcript);
        Assert.Contains("Candidate history 3/3.", fixture.Transcript);
    }

    [Fact]
    public async Task Explicit_compare_replaces_the_pair_and_branches_from_the_history_cursor()
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? oldPhrase = null;
        Pattern? newGroove = null;
        Pattern? newPhrase = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () => fixture.AssertStoppedSelection(fixture.Candidates.Candidate!, accepted, 3, 3)),
            ("back", () => oldPhrase = fixture.Candidates.Candidate!),
            ("compare 456", () =>
            {
                newGroove = fixture.Candidates.Candidate!;
                Assert.Equal(456UL, newGroove.Recipe!.Seed);
                Assert.Equal(GrooveVocabularyGenerator.GeneratorId, newGroove.Recipe.GeneratorId);
                fixture.AssertStoppedSelection(newGroove, accepted, 4, 4);
            }),
            ("compare", () =>
            {
                newPhrase = fixture.Candidates.Candidate!;
                Assert.Equal(456UL, newPhrase.Recipe!.Seed);
                Assert.Equal(StructuredPhraseGenerator.GeneratorId, newPhrase.Recipe.GeneratorId);
                fixture.AssertStoppedSelection(newPhrase, accepted, 4, 4);
            }),
            ("back", () => fixture.AssertStoppedSelection(newPhrase!, accepted, 4, 3)),
            ("back", () => fixture.AssertStoppedSelection(oldPhrase!, accepted, 4, 2)),
            ("forward", () => fixture.AssertStoppedSelection(newPhrase!, accepted, 4, 3)),
            ("forward", () => fixture.AssertStoppedSelection(newGroove!, accepted, 4, 4))));

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Fact]
    public async Task New_after_browsing_back_discards_the_forward_branch_without_changing_the_safe_point()
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? first = null;
        Pattern? discarded = null;
        Pattern? branch = null;

        await fixture.RunAsync(new ScriptedReader(
            ("new 101", () => first = fixture.Candidates.Candidate!),
            ("new 202", () => discarded = fixture.Candidates.Candidate!),
            ("back", () => fixture.AssertStoppedSelection(first!, accepted, 3, 2)),
            ("new 303", () =>
            {
                branch = fixture.Candidates.Candidate!;
                Assert.Equal(303UL, branch.Recipe!.Seed);
                Assert.NotEqual(discarded!.Id, branch.Id);
                fixture.AssertStoppedSelection(branch, accepted, 3, 3);
            }),
            ("forward", () =>
            {
                fixture.AssertStoppedSelection(branch!, accepted, 3, 3);
                Assert.Contains("Error: There is no next audition candidate.", fixture.Transcript);
            }),
            ("back", () => fixture.AssertStoppedSelection(first!, accepted, 3, 2)),
            ("back", () => fixture.AssertStoppedSelection(accepted, accepted, 3, 1)),
            ("forward", () => fixture.AssertStoppedSelection(first!, accepted, 3, 2)),
            ("forward", () => fixture.AssertStoppedSelection(branch!, accepted, 3, 3))));

        Assert.Single(fixture.Transcript.Split('\n'), line => line.Contains("Error:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("back", "previous")]
    [InlineData("forward", "next")]
    public async Task Browsing_past_a_history_boundary_preserves_selection_and_keeps_commands_available(string command, string direction)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;

        await fixture.RunAsync(new ScriptedReader(
            (command, () =>
            {
                fixture.AssertStoppedSelection(accepted, accepted, 1, 1);
                Assert.Contains($"Error: There is no {direction} audition candidate.", fixture.Transcript);
            }),
            ("new 456", () =>
            {
                Assert.Equal(456UL, fixture.Candidates.Candidate!.Recipe!.Seed);
                fixture.AssertStoppedSelection(fixture.Candidates.Candidate, accepted, 2, 2);
            })));
    }

    [Theory]
    [InlineData("middle", false)]
    [InlineData("high", true)]
    public async Task Comparison_creation_rejects_non_bass_roles_and_preserves_session_state(string role, bool explicitSeed)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? candidate = null;
        var controls = fixture.ControlSnapshot();

        await fixture.RunAsync(new ScriptedReader(
            ($"role {role}", () => candidate = fixture.Candidates.Candidate!),
            (explicitSeed ? "compare 456" : "compare", () =>
            {
                fixture.AssertStoppedSelection(candidate!, accepted, 2, 2);
                Assert.Equal(controls, fixture.ControlSnapshot());
                Assert.Contains("Error: Groove comparison currently requires the bass role.", fixture.Transcript);
            }),
            ("revert", () => fixture.AssertStoppedSelection(accepted, accepted, 2, 2)),
            ("compare 789", () =>
            {
                Assert.Equal(789UL, fixture.Candidates.Candidate!.Recipe!.Seed);
                fixture.AssertStoppedSelection(fixture.Candidates.Candidate, accepted, 4, 4);
            })));

        // Bare compare currently consumes its seed before validating the role.
        var seeds = RandomDefaults.CreateRandomSource(123);
        if (!explicitSeed) seeds.NextULong();
        Assert.Equal(seeds.NextULong(), fixture.Seeds.NextULong());
        Assert.Empty(fixture.Port.Messages);
    }

    [Theory]
    [InlineData("compare invalid")]
    [InlineData("compare -1")]
    [InlineData("compare 18446744073709551616")]
    public async Task Invalid_comparison_seed_preserves_the_pair_and_toggle_selection(string command)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? groove = null;
        Pattern? phrase = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () => groove = fixture.Candidates.Candidate!),
            ("compare", () => phrase = fixture.Candidates.Candidate!),
            (command, () =>
            {
                fixture.AssertStoppedSelection(phrase!, accepted, 3, 3);
                Assert.Contains("Error:", fixture.Transcript);
            }),
            ("compare", () => fixture.AssertStoppedSelection(groove!, accepted, 3, 3))));

        Assert.Single(fixture.Transcript.Split('\n'), line => line.Contains("Error:", StringComparison.Ordinal));
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Fact]
    public async Task Failed_non_bass_replacement_retains_the_existing_comparison_for_bare_toggling()
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;
        Pattern? groove = null;
        Pattern? phrase = null;
        Pattern? high = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () => groove = fixture.Candidates.Candidate!),
            ("compare", () => phrase = fixture.Candidates.Candidate!),
            ("role high", () => high = fixture.Candidates.Candidate!),
            ("compare 456", () =>
            {
                fixture.AssertStoppedSelection(high!, accepted, 4, 4);
                Assert.Contains("Error: Groove comparison currently requires the bass role.", fixture.Transcript);
            }),
            ("compare", () => fixture.AssertStoppedSelection(groove!, accepted, 4, 4)),
            ("compare", () => fixture.AssertStoppedSelection(phrase!, accepted, 4, 4))));
    }

    [Fact]
    public async Task Keep_and_revert_use_one_safe_point_without_changing_comparison_history()
    {
        await using var fixture = new Session();
        var initial = fixture.Candidates.Accepted!;
        Pattern? groove = null;
        Pattern? phrase = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () => groove = fixture.Candidates.Candidate!),
            ("keep", () => fixture.AssertStoppedSelection(groove!, groove!, 3, 3)),
            ("compare", () =>
            {
                phrase = fixture.Candidates.Candidate!;
                fixture.AssertStoppedSelection(phrase, groove!, 3, 3);
            }),
            ("revert", () => fixture.AssertStoppedSelection(groove!, groove!, 3, 3)),
            ("back", () => fixture.AssertStoppedSelection(phrase!, groove!, 3, 2)),
            ("keep", () => fixture.AssertStoppedSelection(phrase!, phrase!, 3, 2)),
            ("forward", () => fixture.AssertStoppedSelection(groove!, phrase!, 3, 3)),
            ("revert", () => fixture.AssertStoppedSelection(phrase!, phrase!, 3, 3)),
            ("back", () => fixture.AssertStoppedSelection(phrase!, phrase!, 3, 2)),
            ("back", () => fixture.AssertStoppedSelection(initial, phrase!, 3, 1))));

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Contains("Candidate kept as the safe point.", fixture.Transcript);
        Assert.Contains("Returning to the safe point.", fixture.Transcript);
    }

    [Fact]
    public async Task Running_comparison_and_history_selection_wait_for_a_bar_before_keep_can_move_the_safe_point()
    {
        await using var fixture = new Session();
        var initial = fixture.Candidates.Accepted!;
        fixture.Transport.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Transport.Process(ClockSource.External, RealtimeMessage.Clock);
        Pattern? groove = null;
        Pattern? phrase = null;

        await fixture.RunAsync(new ScriptedReader(
            ("compare 123", () =>
            {
                groove = fixture.Candidates.Candidate!;
                Assert.Same(initial, fixture.Player.CurrentPattern);
                Assert.Same(groove, fixture.Player.PendingPattern);
                Assert.Same(initial, fixture.Candidates.Accepted);
            }),
            ("keep", () =>
            {
                Assert.Contains("Error: The candidate must become audible before it can be accepted.", fixture.Transcript);
                Assert.Same(initial, fixture.Candidates.Accepted);
                Assert.Same(groove, fixture.Player.PendingPattern);
                // Pulse zero was already accepted. Stop immediately before the next bar.
                for (var pulse = 1; pulse < 96; pulse++)
                    fixture.Transport.Process(ClockSource.External, RealtimeMessage.Clock);
                Assert.Same(initial, fixture.Player.CurrentPattern);
                Assert.Same(groove, fixture.Player.PendingPattern);
                fixture.Transport.Process(ClockSource.External, RealtimeMessage.Clock);
                Assert.Same(groove, fixture.Player.CurrentPattern);
                Assert.Null(fixture.Player.PendingPattern);
                Assert.Same(initial, fixture.Candidates.Accepted);
            }),
            ("keep", () => Assert.Same(groove, fixture.Candidates.Accepted)),
            ("compare", () =>
            {
                phrase = fixture.Candidates.Candidate!;
                Assert.Same(phrase, fixture.Player.PendingPattern);
                Assert.Same(groove, fixture.Player.CurrentPattern);
            }),
            ("revert", () =>
            {
                Assert.Same(groove, fixture.Candidates.Candidate);
                Assert.Same(groove, fixture.Candidates.Accepted);
                Assert.Same(groove, fixture.Player.PendingPattern);
                Assert.Equal(3, fixture.History.Position);
            }),
            ("back", () =>
            {
                Assert.Same(phrase, fixture.Candidates.Candidate);
                Assert.Same(phrase, fixture.Player.PendingPattern);
                Assert.Same(groove, fixture.Player.CurrentPattern);
                Assert.Equal(2, fixture.History.Position);
                for (var pulse = 0; pulse < 96; pulse++)
                    fixture.Transport.Process(ClockSource.External, RealtimeMessage.Clock);
                Assert.Same(phrase, fixture.Player.CurrentPattern);
                Assert.Null(fixture.Player.PendingPattern);
                Assert.Same(groove, fixture.Candidates.Accepted);
            }),
            ("keep", () =>
            {
                Assert.Same(phrase, fixture.Candidates.Accepted);
                Assert.Equal(3, fixture.History.Count);
                Assert.Equal(2, fixture.History.Position);
            })));

        Assert.Single(fixture.Transcript.Split('\n'), line => line.Contains("Error:", StringComparison.Ordinal));
        Assert.Empty(fixture.Port.Messages);
        Assert.Null(fixture.Player.Error);
    }

    private static void AssertEquivalentGeneratedPattern(Pattern expected, Pattern actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.Timing, actual.Timing);
        Assert.Equal(expected.TonalContext, actual.TonalContext);
        Assert.Equal(expected.Role, actual.Role);
        Assert.Equal(expected.Recipe!.GeneratorId, actual.Recipe!.GeneratorId);
        Assert.Equal(expected.Recipe.GeneratorVersion, actual.Recipe.GeneratorVersion);
        Assert.Equal(expected.Recipe.Seed, actual.Recipe.Seed);
        Assert.Equal(expected.Recipe.Parameters.ToArray(), actual.Recipe.Parameters.ToArray());
        Assert.Equal(expected.Steps.Length, actual.Steps.Length);
        for (var index = 0; index < expected.Steps.Length; index++)
        {
            Assert.Equal(expected.Steps[index].Probability, actual.Steps[index].Probability);
            Assert.Equal(expected.Steps[index].Notes, actual.Steps[index].Notes);
        }
    }

    // Checkpoints run on the next read, outside the console's command-error handler,
    // so assertion failures cannot be mistaken for recoverable command errors.
    private sealed class ScriptedReader(params (string Command, Action After)[] steps) : TextReader
    {
        private int index;

        public override Task<string?> ReadLineAsync()
        {
            if (index > 0) steps[index - 1].After();
            return Task.FromResult(index < steps.Length ? steps[index++].Command : null);
        }
    }

    private sealed class Session : IAsyncDisposable
    {
        public SafeMidiOutput Output { get; } = new();
        public TransportEngine Transport { get; } = new();
        public InternalMidiClock Clock { get; }
        public PatternPlayer Player { get; }
        public CandidateSession Candidates { get; }
        public CandidateHistory History { get; } = new();
        public GenerationControls Controls { get; } = new();
        public PatternLibrary Library { get; } = new(Path.Combine(Path.GetTempPath(), $"jam-weaver-command-{Guid.NewGuid():N}"));
        public IRandomSource Seeds { get; } = RandomDefaults.CreateRandomSource(123);
        public RecordingPort Port { get; } = new();
        public int PortOpenAttempts { get; private set; }
        public string Transcript => writer.ToString();
        private readonly StringWriter writer = new();
        private readonly StructuredPhraseGenerator phrase = new();
        private readonly GrooveVocabularyGenerator groove = new();
        private readonly CandidateGenerator generator;

        public Session(bool createInitialCandidate = true)
        {
            Output.ReplacePort(Port);
            Clock = new InternalMidiClock(Output, Transport);
            Player = new PatternPlayer(Output, Transport);
            Candidates = new CandidateSession(Player);
            generator = new CandidateGenerator(new EuclideanMotifGenerator(), new TurnaroundLoopGenerator(),
                new EuclideanPhraseGenerator(), phrase, groove, new MotifGenerator());
            if (createInitialCandidate)
            {
                var initial = generator.Generate(42, null, Controls);
                Candidates.SetCandidate(initial);
                History.Add(initial);
            }
        }

        public object ControlSnapshot() => (Controls.Mode, Controls.PhraseLength, Controls.Activity, Controls.Rhythm,
            Controls.Movement, Controls.Variation, Controls.Turnaround, Controls.Groove, Controls.Similarity, Controls.MotifShape);

        public void AssertStoppedSelection(Pattern candidate, Pattern accepted, int count, int position)
        {
            Assert.Same(candidate, Candidates.Candidate);
            Assert.Same(accepted, Candidates.Accepted);
            Assert.Same(candidate, Player.CurrentPattern);
            Assert.Null(Player.PendingPattern);
            Assert.Equal(TransportState.Stopped, Transport.State);
            Assert.Equal(count, History.Count);
            Assert.Equal(position, History.Position);
            Assert.Equal(position > 1, History.CanPrevious);
            Assert.Equal(position < count, History.CanNext);
        }

        public Task RunAsync(string commands) => RunAsync(new StringReader(commands));

        public Task RunAsync(TextReader reader)
        {
            var catalog = new FakeMidiPortCatalog
            {
                OutputFactory = _ => { PortOpenAttempts++; return new RecordingPort(); },
                InputFactory = _ => { PortOpenAttempts++; throw new InvalidOperationException("unexpected input open"); }
            };
            return new JamWeaverConsole(reader, new ConsoleDisplay(writer), Output, Transport, Clock,
                Player, Candidates, generator, Controls, History, Library, phrase, groove, Seeds,
                new ExternalMidiClockInput(), catalog).RunAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try { await Clock.DisposeAsync(); }
            finally
            {
                Player.Dispose();
                Output.Dispose();
                Library.Dispose();
                if (Directory.Exists(Library.RootDirectory)) Directory.Delete(Library.RootDirectory, recursive: true);
            }
        }
    }

    private sealed class RecordingPort : IMidiOutputPort
    {
        public string Name => "command-test";
        public List<string> Messages { get; } = [];
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => Messages.Add("On");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity) => Messages.Add("Off");
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) => Messages.Add("CC");
        public void SendProgramChange(MidiChannel channel, MidiValue program) => Messages.Add("Program");
        public void SendClock() => Messages.Add("Clock");
        public void SendStart() => Messages.Add("Start");
        public void SendContinue() => Messages.Add("Continue");
        public void SendStop() => Messages.Add("Stop");
        public void Dispose() { }
    }
}
