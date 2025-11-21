using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Service for comparing translations
/// </summary>
public interface ITranslationComparer
{
    /// <summary>
    ///     Compare two translation results
    /// </summary>
    /// <param name="result1">First translation result</param>
    /// <param name="result2">Second translation result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comparison result</returns>
    Task<TranslationComparison> CompareAsync(
        TranslationResult result1,
        TranslationResult result2,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculate Levenshtein edit distance between two strings
    /// </summary>
    /// <param name="text1">First text</param>
    /// <param name="text2">Second text</param>
    /// <returns>Edit distance</returns>
    int CalculateEditDistance(string text1, string text2);

    /// <summary>
    ///     Calculate BLEU score between reference and candidate translation
    /// </summary>
    /// <param name="reference">Reference translation</param>
    /// <param name="candidate">Candidate translation</param>
    /// <returns>BLEU score (0.0 - 1.0)</returns>
    float CalculateBleuScore(string reference, string candidate);
}