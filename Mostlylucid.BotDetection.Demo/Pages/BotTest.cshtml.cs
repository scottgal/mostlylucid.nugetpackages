using Microsoft.AspNetCore.Mvc.RazorPages;
using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Orchestration;

namespace Mostlylucid.BotDetection.Demo.Pages;

public class BotTestModel : PageModel
{
    // Core detection results
    public bool IsBot { get; set; }
    public double Confidence { get; set; }
    public string BotType { get; set; } = "None";
    public string BotName { get; set; } = "None";

    // Risk assessment
    public string RiskBand { get; set; } = "Unknown";
    public string RecommendedAction { get; set; } = "Allow";

    // Detection reasons
    public List<DetectionReasonViewModel> Reasons { get; set; } = new();

    // Category breakdown
    public Dictionary<string, double> CategoryScores { get; set; } = new();

    // Processing info
    public double ProcessingTimeMs { get; set; }
    public int DetectorCount { get; set; }
    public List<string> DetectorsRan { get; set; } = new();

    public void OnGet()
    {
        // Core detection
        IsBot = HttpContext.IsBot();
        Confidence = HttpContext.GetBotConfidence();
        BotType = HttpContext.GetBotType()?.ToString() ?? "None";
        BotName = HttpContext.GetBotName() ?? "None";

        // Risk assessment
        RiskBand = HttpContext.GetRiskBand().ToString();
        RecommendedAction = HttpContext.GetRecommendedAction().ToString();

        // Get detection reasons
        var reasons = HttpContext.GetDetectionReasons();
        Reasons = reasons.Select(r => new DetectionReasonViewModel
        {
            Category = r.Category,
            Detail = r.Detail,
            Impact = r.ConfidenceImpact
        }).ToList();

        // Try to get aggregated evidence for more details
        if (HttpContext.Items.TryGetValue(BotDetectionMiddleware.AggregatedEvidenceKey, out var evidenceObj)
            && evidenceObj is AggregatedEvidence evidence)
        {
            ProcessingTimeMs = evidence.TotalProcessingTimeMs;
            DetectorCount = evidence.ContributingDetectors.Count;
            DetectorsRan = evidence.ContributingDetectors.ToList();

            // Category breakdown
            foreach (var (category, info) in evidence.CategoryBreakdown)
            {
                CategoryScores[category] = Math.Round(info.Score * 100, 1);
            }
        }
    }
}

public class DetectionReasonViewModel
{
    public string Category { get; set; } = "";
    public string Detail { get; set; } = "";
    public double Impact { get; set; }

    public string ImpactClass => Impact switch
    {
        > 0.3 => "high-impact",
        > 0.1 => "medium-impact",
        > 0 => "low-impact",
        < -0.1 => "human-signal",
        _ => "neutral"
    };

    public string ImpactDisplay => Impact switch
    {
        > 0 => $"+{Impact:P0}",
        < 0 => $"{Impact:P0}",
        _ => "0%"
    };
}
