using Microsoft.AspNetCore.Mvc.RazorPages;
using Mostlylucid.BotDetection.Extensions;

namespace Mostlylucid.BotDetection.Demo.Pages;

public class BotTestModel : PageModel
{
    public bool IsBot { get; set; }
    public double Confidence { get; set; }
    public string? BotType { get; set; }
    public string? BotName { get; set; }

    public void OnGet()
    {
        IsBot = HttpContext.IsBot();
        Confidence = HttpContext.GetBotConfidence();
        BotType = HttpContext.GetBotType()?.ToString();
        BotName = HttpContext.GetBotName();
    }
}
