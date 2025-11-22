using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects dates that appear to be dates of birth based on context.
/// </summary>
public class DateOfBirthDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.DateOfBirth;
    public override string Name => "DateOfBirthDetector";
    public override int Priority => 70;
    protected override double DefaultConfidence => 0.6;

    // Common date formats
    // DD/MM/YYYY, MM/DD/YYYY, YYYY-MM-DD, DD-MM-YYYY, etc.
    protected override string Pattern =>
        @"(?<!\d)(?:(?:0?[1-9]|[12]\d|3[01])[/\-.](?:0?[1-9]|1[0-2])[/\-.](?:19|20)\d{2}|(?:19|20)\d{2}[/\-.](?:0?[1-9]|1[0-2])[/\-.](?:0?[1-9]|[12]\d|3[01])|(?:0?[1-9]|1[0-2])[/\-.](?:0?[1-9]|[12]\d|3[01])[/\-.](?:19|20)\d{2})(?!\d)";

    protected override bool ValidateMatch(Match match, string originalText)
    {
        // Check if context suggests this is a date of birth, not just any date
        var contextStart = Math.Max(0, match.Index - 50);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 30);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var dobKeywords = new[]
        {
            "dob", "date of birth", "birth date", "birthdate", "born", "birthday",
            "d.o.b", "date-of-birth", "birth_date", "birth-date"
        };

        return dobKeywords.Any(context.Contains);
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var confidence = 0.7;

        // Parse the date to validate it's reasonable for DOB
        var dateStr = match.Value;
        if (TryParseDate(dateStr, out var date))
        {
            var age = DateTime.Now.Year - date.Year;

            // Reasonable age range (0-120)
            if (age is >= 0 and <= 120)
                confidence += 0.1;

            // Very common age range (18-80)
            if (age is >= 18 and <= 80)
                confidence += 0.1;
        }

        return Math.Clamp(confidence, 0.0, 1.0);
    }

    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;

        var formats = new[]
        {
            "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MM-yyyy",
            "d/M/yyyy", "M/d/yyyy", "yyyy/MM/dd", "dd.MM.yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date))
            {
                return true;
            }
        }

        return DateTime.TryParse(dateStr, out date);
    }
}
