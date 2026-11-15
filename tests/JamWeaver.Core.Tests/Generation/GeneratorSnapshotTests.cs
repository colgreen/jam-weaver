using System.Security.Cryptography;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Persistence;

namespace JamWeaver.Core.Tests.Generation;

public sealed class GeneratorSnapshotTests
{
    [Theory]
    [InlineData(MusicalRole.Bass, MotifShape.Auto, PhraseActivity.Sparse, PhraseLevel.Low, PhraseLevel.Low, PitchPalette.MinorPentatonic, 101UL, "AED3D2C126C4B8F2133808429CF0E82363184EAF6CFC17DA00F24C5F7A22DAD7")]
    [InlineData(MusicalRole.Middle, MotifShape.Auto, PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Medium, PitchPalette.MajorPentatonic, 201UL, "1549A2A8225457FBB9B79F0C8032EAFC04526CABF2E26DEAEE3C38C3E91DDA99")]
    [InlineData(MusicalRole.High, MotifShape.Auto, PhraseActivity.Busy, PhraseLevel.High, PhraseLevel.High, PitchPalette.MinorPentatonic, 301UL, "900B474EA5312131063AA9F2D0B624FBE3597E4274B78A5C4375C423F3C3244F")]
    [InlineData(MusicalRole.Bass, MotifShape.Pedal, PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Medium, PitchPalette.MinorPentatonic, 102UL, "661676390AF898A7869B92D6F916D1FA106828829F463CF193B58CC74DBCE3A0")]
    [InlineData(MusicalRole.Middle, MotifShape.RootFifth, PhraseActivity.Busy, PhraseLevel.Low, PhraseLevel.Medium, PitchPalette.MajorPentatonic, 202UL, "7286EAD633C4C19C36CAE3BFD5AEC628A7BECE07EDCACFFB780EAE724546A948")]
    [InlineData(MusicalRole.High, MotifShape.Walking, PhraseActivity.Sparse, PhraseLevel.High, PhraseLevel.Low, PitchPalette.MinorPentatonic, 302UL, "8D527F571A031E507BCD661EEDD316342E330DEC6AF92F2CB6F92F607E4F2E2A")]
    [InlineData(MusicalRole.Bass, MotifShape.CallResponse, PhraseActivity.Busy, PhraseLevel.High, PhraseLevel.High, PitchPalette.MajorPentatonic, 103UL, "9D8DB36A487BF8D3B6C8C70285863F670DA8DA7571AEC65465C27582CD218373")]
    [InlineData(MusicalRole.Middle, MotifShape.Arch, PhraseActivity.Sparse, PhraseLevel.Medium, PhraseLevel.Low, PitchPalette.MinorPentatonic, 203UL, "5B395F9377EDBD0992C525ECAF4BA8B72B9532DC5CF097A6599837E5E9BBA9FC")]
    [InlineData(MusicalRole.High, MotifShape.Pickup, PhraseActivity.Medium, PhraseLevel.Low, PhraseLevel.High, PitchPalette.MajorPentatonic, 303UL, "A2DC60F4BCCEB6429A575310D3B851E1300FA5BCFC5DCFE2B7711F115DDE563C")]
    [InlineData(MusicalRole.Bass, MotifShape.Riff, PhraseActivity.Medium, PhraseLevel.High, PhraseLevel.High, PitchPalette.MinorPentatonic, 104UL, "2F3B0B11866F83A80E6E3F847BE06295445CB869CDD2F1D60BFED5EA4A8BE9EB")]
    [InlineData(MusicalRole.Middle, MotifShape.Walking, PhraseActivity.Busy, PhraseLevel.High, PhraseLevel.High, PitchPalette.MinorPentatonic, 204UL, "A50B7BA6A07EE6073F26CA09527D2A5F60D367BE0485F1F49C89D67BA6B49977")]
    [InlineData(MusicalRole.High, MotifShape.RootFifth, PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Medium, PitchPalette.MajorPentatonic, 304UL, "F6484A3C086A05169DBAB1557D0892381B38A1B4C7B667D916BE68D5207D54CF")]
    public void Motif_version_4_preserves_material_and_recipe(MusicalRole role, MotifShape shape,
        PhraseActivity activity, PhraseLevel movement, PhraseLevel variation, PitchPalette palette,
        ulong seed, string expectedHash)
    {
        var settings = new MotifGeneratorSettings(new PatternName("Motif snapshot"),
            new TonalContext(new RootPitchClass(2), palette), role, shape, activity, movement, variation, seed);

        var pattern = new MotifGenerator().Generate(settings);

        Assert.Equal("melodic-musical-motif", pattern.Recipe!.GeneratorId);
        Assert.Equal(4, pattern.Recipe.GeneratorVersion);
        AssertSnapshot(pattern, expectedHash);
    }

    [Theory]
    [InlineData(GrooveSelection.Auto, PhraseActivity.Sparse, PhraseLevel.Low, PhraseLevel.Low, GrooveSimilarity.Close, PhraseTurnaround.None, 101UL, "10A9B1FC0EAF396666BFB368B67ACA3025DC9D26D37E57BF039BC4EA044C0614")]
    [InlineData(GrooveSelection.Foundation, PhraseActivity.Busy, PhraseLevel.High, PhraseLevel.High, GrooveSimilarity.Contrast, PhraseTurnaround.Strong, 102UL, "58256AA781F4D956BAFD46F126F36DA087B01B2E6F786F61E43BC3232A8F9C23")]
    [InlineData(GrooveSelection.Offbeat, PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.High, GrooveSimilarity.Related, PhraseTurnaround.Subtle, 103UL, "BB73F2AA25D4D0EB30F5AA89F8C53026E44E81C2C0B75DDEABBEC44578929D4A")]
    [InlineData(GrooveSelection.Anticipation, PhraseActivity.Sparse, PhraseLevel.High, PhraseLevel.Medium, GrooveSimilarity.Close, PhraseTurnaround.None, 104UL, "2D3532509DD0456637F1EA70A3E7C96F1A74A4B1B7C7FFC0641B036A07B1DFF6")]
    [InlineData(GrooveSelection.LongShort, PhraseActivity.Busy, PhraseLevel.Low, PhraseLevel.High, GrooveSimilarity.Contrast, PhraseTurnaround.Strong, 105UL, "CA111735A862D93CFA62158CAE8921A6538750F382A915B277D98C278F074D2D")]
    [InlineData(GrooveSelection.SparseAnswer, PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Low, GrooveSimilarity.Related, PhraseTurnaround.Subtle, 106UL, "A502324D52837D9C930CB9CC4B7BBFFE48CE0359A8765D7ABCC61A02AAAC3710")]
    [InlineData(GrooveSelection.Broken, PhraseActivity.Busy, PhraseLevel.High, PhraseLevel.High, GrooveSimilarity.Close, PhraseTurnaround.None, 107UL, "7985B5F658D456EAFEF5B0EB88808ABF0A37847EC8D0998DF5A4A398EA44679E")]
    public void Groove_version_1_preserves_material_and_recipe(GrooveSelection groove, PhraseActivity activity,
        PhraseLevel movement, PhraseLevel variation, GrooveSimilarity similarity, PhraseTurnaround turnaround,
        ulong seed, string expectedHash)
    {
        var settings = new GrooveGeneratorSettings(new PatternName("Groove snapshot"),
            new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic), MusicalRole.Bass,
            groove, similarity, activity, movement, variation, turnaround, seed);

        var pattern = new GrooveVocabularyGenerator().Generate(settings);

        Assert.Equal("melodic-groove-vocabulary", pattern.Recipe!.GeneratorId);
        Assert.Equal(1, pattern.Recipe.GeneratorVersion);
        AssertSnapshot(pattern, expectedHash);
    }

    private static void AssertSnapshot(Pattern pattern, string expectedHash)
    {
        // Only the random snapshot ID and save time are normalized; every musical and recipe value is retained.
        var snapshot = new Pattern(new PatternId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            pattern.Name, pattern.SchemaVersion, pattern.Mode, pattern.Timing, pattern.Steps,
            pattern.Role, pattern.TonalContext, pattern.Recipe);
        var codec = new PatternJsonCodec();
        var data = codec.Encode(snapshot, DateTimeOffset.UnixEpoch);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(data)));
    }
}
