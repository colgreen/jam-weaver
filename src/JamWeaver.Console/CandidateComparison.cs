using Redzen.Random;

namespace JamWeaver.ConsoleApp;

/// <summary>Prepares and auditions matched phrase/groove comparisons while retaining their toggle selection.</summary>
internal sealed class CandidateComparison
{
    private readonly StructuredPhraseGenerator phraseGenerator;
    private readonly GrooveVocabularyGenerator grooveGenerator;
    private readonly IRandomSource seedSource;
    private readonly GenerationControls generationControls;
    private readonly CandidateSession session;
    private readonly CandidateHistory history;
    private readonly ConsoleDisplay display;
    private Pattern? phrase;
    private Pattern? groove;
    private bool showsGroove = true;

    public CandidateComparison(StructuredPhraseGenerator phraseGenerator, GrooveVocabularyGenerator grooveGenerator,
        IRandomSource seedSource, GenerationControls generationControls, CandidateSession session,
        CandidateHistory history, ConsoleDisplay display)
    {
        this.phraseGenerator = phraseGenerator ?? throw new ArgumentNullException(nameof(phraseGenerator));
        this.grooveGenerator = grooveGenerator ?? throw new ArgumentNullException(nameof(grooveGenerator));
        this.seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
        this.generationControls = generationControls ?? throw new ArgumentNullException(nameof(generationControls));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.display = display ?? throw new ArgumentNullException(nameof(display));
    }

    /// <summary>Prepares a new pair with an explicit seed, or toggles the existing pair when the seed is omitted.</summary>
    /// <remarks>If no pair exists, an omitted seed prepares one using the shared seed source.</remarks>
    public void Compare(ulong? seed)
    {
        if (seed.HasValue || phrase is null || groove is null)
        {
            var compareSeed = seed ?? seedSource.NextULong();
            var current = session.Candidate;
            var context = current?.TonalContext ?? CandidateGenerator.DefaultTonalContext();
            var role = current?.Role ?? MusicalRole.Bass;
            if (role != MusicalRole.Bass)
                throw new InvalidOperationException("Groove comparison currently requires the bass role.");
            var name = new PatternName($"Compare {compareSeed}");
            phrase = phraseGenerator.Generate(new PhraseGeneratorSettings(name, PhraseLength.FourBars,
                context, role, generationControls.Activity, generationControls.Rhythm,
                generationControls.Movement, generationControls.Variation, generationControls.Turnaround, compareSeed));
            groove = grooveGenerator.Generate(new GrooveGeneratorSettings(name, context, role,
                generationControls.Groove, generationControls.Similarity, generationControls.Activity,
                generationControls.Movement, generationControls.Variation, generationControls.Turnaround, compareSeed));
            history.Add(phrase);
            history.Add(groove);
            showsGroove = false;
            display.WriteLine($"Prepared matched comparison with seed {compareSeed}.");
        }
        showsGroove = !showsGroove;
        session.SetCandidate(showsGroove ? groove : phrase);
        display.WriteLine($"Comparison: {(showsGroove ? "groove" : "phrase")} (changes at next bar while running).");
    }
}
