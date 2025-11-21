using Microsoft.Extensions.Logging;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Service for comparing translations
/// </summary>
public class TranslationComparer : ITranslationComparer
{
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<TranslationComparer> _logger;

    public TranslationComparer(
        ILogger<TranslationComparer> logger,
        IEmbeddingGenerator embeddingGenerator)
    {
        _logger = logger;
        _embeddingGenerator = embeddingGenerator;
    }

    public Task<TranslationComparison> CompareAsync(
        TranslationResult result1,
        TranslationResult result2,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Comparing translations for document {DocumentId} using methods {Method1} vs {Method2}",
            result1.DocumentId, result1.Method, result2.Method);

        var comparison = new TranslationComparison
        {
            DocumentId = result1.DocumentId,
            Translation1 = result1,
            Translation2 = result2
        };

        // Ensure both have the same number of blocks
        var maxBlocks = Math.Max(result1.Blocks.Count, result2.Blocks.Count);

        for (var i = 0; i < maxBlocks; i++)
        {
            var block1 = i < result1.Blocks.Count ? result1.Blocks[i] : null;
            var block2 = i < result2.Blocks.Count ? result2.Blocks[i] : null;

            if (block1 == null || block2 == null) continue;

            var text1 = block1.TranslatedText ?? block1.Text;
            var text2 = block2.TranslatedText ?? block2.Text;

            var editDistance = CalculateEditDistance(text1, text2);
            var maxLength = Math.Max(text1.Length, text2.Length);
            var similarity = maxLength > 0 ? 1.0f - (float)editDistance / maxLength : 1.0f;

            var difference = new BlockDifference
            {
                BlockIndex = i,
                Text1 = text1,
                Text2 = text2,
                Similarity = similarity,
                EditDistance = editDistance
            };

            comparison.Differences.Add(difference);
        }

        // Calculate overall similarity
        if (comparison.Differences.Count > 0)
            comparison.SimilarityScore = comparison.Differences.Average(d => d.Similarity);

        _logger.LogInformation(
            "Comparison completed. Overall similarity: {Similarity:F2}",
            comparison.SimilarityScore);

        return Task.FromResult(comparison);
    }

    public int CalculateEditDistance(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1))
            return text2?.Length ?? 0;

        if (string.IsNullOrEmpty(text2))
            return text1.Length;

        var len1 = text1.Length;
        var len2 = text2.Length;

        var matrix = new int[len1 + 1, len2 + 1];

        // Initialize first column and row
        for (var i = 0; i <= len1; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= len2; j++)
            matrix[0, j] = j;

        // Calculate distances
        for (var i = 1; i <= len1; i++)
        for (var j = 1; j <= len2; j++)
        {
            var cost = text1[i - 1] == text2[j - 1] ? 0 : 1;

            matrix[i, j] = Math.Min(
                Math.Min(
                    matrix[i - 1, j] + 1, // Deletion
                    matrix[i, j - 1] + 1), // Insertion
                matrix[i - 1, j - 1] + cost); // Substitution
        }

        return matrix[len1, len2];
    }

    public float CalculateBleuScore(string reference, string candidate)
    {
        // Simplified BLEU-1 score (unigram precision)
        // For a full BLEU implementation, consider using a library

        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(candidate)) return 0;

        var refTokens = reference.ToLower().Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);
        var candTokens = candidate.ToLower().Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);

        if (candTokens.Length == 0) return 0;

        var refSet = new HashSet<string>(refTokens);
        var matchCount = candTokens.Count(token => refSet.Contains(token));

        var precision = (float)matchCount / candTokens.Length;

        // Brevity penalty
        var brevityPenalty = candTokens.Length < refTokens.Length
            ? MathF.Exp(1 - (float)refTokens.Length / candTokens.Length)
            : 1.0f;

        return precision * brevityPenalty;
    }
}