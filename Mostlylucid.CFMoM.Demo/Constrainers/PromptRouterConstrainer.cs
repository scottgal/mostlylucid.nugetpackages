using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.Constrainers;

namespace Mostlylucid.CFMoM.Demo.Constrainers;

/// <summary>
///     Prompt router constrainer.
///     Routes prompts to appropriate handlers based on aggregated signals.
/// </summary>
public sealed class PromptRouterConstrainer : IConstrainer<PromptContext, PromptRoutingDecision>
{
    public ConstrainerResult<PromptRoutingDecision> Evaluate(AggregatedResult result, PromptContext context)
    {
        // Handle early exit first
        if (result.EarlyExit)
        {
            var earlyDecision = MapEarlyExitToDecision(result.EarlyExitClassification);
            return ConstrainerResult<PromptRoutingDecision>.Stop(
                earlyDecision,
                $"Early exit: {result.EarlyExitClassification}",
                "early-exit");
        }

        // Check immediate block threshold (very high risk)
        if (result.Score >= 0.85)
        {
            return ConstrainerResult<PromptRoutingDecision>.Stop(
                PromptRoutingDecision.Block,
                $"High risk score: {result.Score:P0}",
                "immediate-block");
        }

        // Check challenge threshold (needs clarification)
        if (result.Score >= 0.65)
        {
            return ConstrainerResult<PromptRoutingDecision>.Stop(
                PromptRoutingDecision.Challenge,
                $"Moderate risk score: {result.Score:P0}",
                "challenge");
        }

        // Check if we have enough confidence for a decision
        if (result.Confidence < 0.4)
        {
            // Low confidence, continue gathering more signals
            return ConstrainerResult<PromptRoutingDecision>.Continue(
                PromptRoutingDecision.Allow,
                $"Low confidence ({result.Confidence:P0}), gathering more evidence");
        }

        // Low risk, allow with routing
        return ConstrainerResult<PromptRoutingDecision>.Stop(
            PromptRoutingDecision.Allow,
            $"Low risk score: {result.Score:P0}",
            "allow");
    }

    private static PromptRoutingDecision MapEarlyExitToDecision(string? classification)
    {
        return classification?.ToLowerInvariant() switch
        {
            "whitelisted" or "allow" => PromptRoutingDecision.Allow,
            "blacklisted" or "block" => PromptRoutingDecision.Block,
            "challenge" => PromptRoutingDecision.Challenge,
            _ => PromptRoutingDecision.Allow
        };
    }
}

/// <summary>
///     Routing decision for prompts.
/// </summary>
public enum PromptRoutingDecision
{
    /// <summary>Allow the prompt to be processed.</summary>
    Allow,
    /// <summary>Block the prompt.</summary>
    Block,
    /// <summary>Challenge the user for clarification.</summary>
    Challenge,
    /// <summary>Escalate to human review.</summary>
    Escalate
}
