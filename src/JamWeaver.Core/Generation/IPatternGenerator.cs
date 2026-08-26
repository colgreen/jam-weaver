using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Generation;

public interface IPatternGenerator<in TSettings>
{
    Pattern Generate(TSettings settings);
}
