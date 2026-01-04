namespace Mostlylucid.CFMoM.Demo.Retrieval;

/// <summary>
///     Salience scorer for measuring importance based on past usage.
///     Uses confidence × log(hitCount + 1) formula.
/// </summary>
public static class SalienceScorer
{
    /// <summary>
    ///     Calculate salience score for an item.
    /// </summary>
    /// <param name="confidence">The confidence level (0-1).</param>
    /// <param name="hitCount">Number of times this item has been matched.</param>
    /// <returns>Salience score.</returns>
    public static double Calculate(double confidence, int hitCount)
    {
        return confidence * Math.Log(hitCount + 1 + 1); // +1 for log, +1 for minimum contribution
    }

    /// <summary>
    ///     Calculate salience with optional recency decay.
    /// </summary>
    /// <param name="confidence">The confidence level (0-1).</param>
    /// <param name="hitCount">Number of times this item has been matched.</param>
    /// <param name="lastUsed">When this item was last used.</param>
    /// <param name="decayDays">Days after which decay starts.</param>
    /// <returns>Salience score with recency factor.</returns>
    public static double CalculateWithRecency(
        double confidence,
        int hitCount,
        DateTimeOffset lastUsed,
        int decayDays = 30)
    {
        var baseSalience = Calculate(confidence, hitCount);

        // Apply recency decay
        var daysSinceUse = (DateTimeOffset.UtcNow - lastUsed).TotalDays;
        var recencyFactor = daysSinceUse <= decayDays
            ? 1.0
            : Math.Exp(-(daysSinceUse - decayDays) / 30); // Exponential decay after threshold

        return baseSalience * recencyFactor;
    }
}
