using Mostlylucid.CFMoM.Demo.Embedding;
using Mostlylucid.CFMoM.Demo.Models;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     Learning proposer that checks for similar past decisions.
///     Runs in Wave -1 (highest priority) for fast path on known patterns.
/// </summary>
public sealed class LearningProposer : ProposerBase<PromptContext>
{
    private readonly ILearningStore _learningStore;
    private readonly IEmbeddingService _embeddingService;

    private const double MinSimilarity = 0.85;
    private const double MinFactMatch = 0.80;

    public override string Name => "learning-cache";
    public override string FactsSchemaId => LearnedFacts.SchemaId;
    public override int Priority => -100; // Very high priority (runs first)

    public LearningProposer(ILearningStore learningStore, IEmbeddingService embeddingService)
    {
        _learningStore = learningStore;
        _embeddingService = embeddingService;
    }

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<PromptContext> state,
        CancellationToken cancellationToken = default)
    {
        var context = state.Context;

        // Generate embedding for the prompt
        var embedding = context.Embedding ?? _embeddingService.Embed(context.Prompt);

        // Search for similar decisions
        var candidates = await _learningStore.SearchSimilarAsync(
            embedding,
            context.Prompt,
            topK: 5,
            minSimilarity: MinSimilarity,
            cancellationToken);

        if (candidates.Count == 0)
            return None();

        // Extract quick facts from current prompt for verification
        var currentFacts = QuickFactExtractor.Extract(context.Prompt);

        // Try to find a confirmed match
        foreach (var candidate in candidates)
        {
            var storedFacts = await _learningStore.GetFactsAsync(candidate.Id, cancellationToken);
            var factMatchScore = QuickFactExtractor.CompareFactsMatch(currentFacts, storedFacts);

            var similarity = _embeddingService.CosineSimilarity(embedding, candidate.PromptEmbedding);

            // Check if match is confirmed
            if (similarity >= MinSimilarity && factMatchScore >= MinFactMatch)
            {
                // Increment hit count asynchronously
                _ = _learningStore.IncrementHitCountAsync(candidate.Id, cancellationToken);

                var facts = new LearnedFacts
                {
                    MatchedDecisionId = candidate.Id,
                    SimilarityScore = similarity,
                    MatchedPromptText = candidate.PromptText,
                    FactMatchScore = factMatchScore,
                    LearnedDecision = candidate.Decision,
                    Reason = $"Matched pattern (similarity: {similarity:P0}, fact match: {factMatchScore:P0})",
                    HitCount = candidate.HitCount + 1
                };

                // Create early exit signal with learned classification
                var signal = CreateSignal(facts, (float)candidate.Confidence)
                    .WithEarlyExit(MapDecisionToClassification(candidate.Decision))
                    .WithMetadata("orchestration_signals", new Dictionary<string, object>
                    {
                        ["learned_match"] = true,
                        ["learned_decision"] = candidate.Decision,
                        ["similarity"] = similarity,
                        ["fact_match"] = factMatchScore
                    });

                return Single(signal);
            }
        }

        // No confirmed match found, return nothing
        return None();
    }

    private static string MapDecisionToClassification(string decision)
    {
        return decision.ToLowerInvariant() switch
        {
            "allow" => "whitelisted",
            "block" => "blacklisted",
            "challenge" => "challenge",
            _ => "whitelisted"
        };
    }
}
