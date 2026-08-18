using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Tests.Performance;

public sealed class CandidateHistoryTests
{
    [Fact]
    public void History_caps_capacity_and_navigates()
    {
        var history = new CandidateHistory(3);
        var patterns = Enumerable.Range(0, 4).Select(Pattern).ToArray();
        foreach (var pattern in patterns) history.Add(pattern);

        Assert.Equal(3, history.Count);
        Assert.Equal(patterns[2].Id, history.Previous().Id);
        Assert.Equal(patterns[1].Id, history.Previous().Id);
        Assert.False(history.CanPrevious);
        Assert.Equal(patterns[2].Id, history.Next().Id);
    }

    [Fact]
    public void Adding_after_previous_discards_forward_branch()
    {
        var history = new CandidateHistory();
        history.Add(Pattern(1));
        history.Add(Pattern(2));
        _ = history.Previous();
        var branch = Pattern(3);

        history.Add(branch);

        Assert.False(history.CanNext);
        Assert.Equal("p1", history.Previous().Name.Value);
        Assert.Equal(branch.Id, history.Next().Id);
    }

    private static Pattern Pattern(int note) => new(PatternId.New(), new PatternName($"p{note}"),
        PatternSchemaVersion.Current, PatternMode.Drums, PatternTiming.SixteenthNotes,
        [new PatternStep([new PatternNote(new DrumPitch(new MidiValue(note)), new MidiValue(100), new NoteGate(.5))], TriggerProbability.Always)], null, null);
}
