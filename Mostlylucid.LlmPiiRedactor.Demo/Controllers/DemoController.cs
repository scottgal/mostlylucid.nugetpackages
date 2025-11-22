using Microsoft.AspNetCore.Mvc;
using Mostlylucid.LlmPiiRedactor.Filters;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;

namespace Mostlylucid.LlmPiiRedactor.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IPiiRedactionService _redactionService;
    private readonly ILogger<DemoController> _logger;

    public DemoController(IPiiRedactionService redactionService, ILogger<DemoController> logger)
    {
        _redactionService = redactionService;
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates redaction with different styles.
    /// </summary>
    [HttpGet("compare-styles")]
    public IActionResult CompareStyles([FromQuery] string text = "Contact john.doe@example.com or call +1-555-123-4567")
    {
        var results = new Dictionary<string, string>();

        // Detect PII first
        var matches = _redactionService.Detect(text);

        return Ok(new
        {
            originalText = text,
            detectedPii = matches.Select(m => new
            {
                type = m.Type.ToString(),
                value = m.OriginalValue,
                position = m.StartIndex,
                confidence = m.Confidence
            }),
            redactedText = _redactionService.Redact(text).RedactedText
        });
    }

    /// <summary>
    /// Tests credit card detection with Luhn validation.
    /// </summary>
    [HttpGet("test-credit-card")]
    public IActionResult TestCreditCard([FromQuery] string cardNumber = "4111111111111111")
    {
        var testText = $"Payment card: {cardNumber}";
        var result = _redactionService.Redact(testText);

        return Ok(new
        {
            input = testText,
            output = result.RedactedText,
            detected = result.ContainedPii,
            cardType = result.Matches.FirstOrDefault()?.Type.ToString(),
            confidence = result.Matches.FirstOrDefault()?.Confidence
        });
    }

    /// <summary>
    /// Tests detection of multiple PII types in a single text.
    /// </summary>
    [HttpPost("test-multiple")]
    public IActionResult TestMultiple([FromBody] MultiPiiRequest request)
    {
        var result = _redactionService.Redact(request.Text);

        _logger.LogInformation("Processed text with {Count} PII instances", result.Matches.Count);

        return Ok(new
        {
            original = result.OriginalText,
            redacted = result.RedactedText,
            statistics = new
            {
                totalMatches = result.Matches.Count,
                uniqueTypes = result.UniqueTypesCount,
                typeCounts = result.TypeCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
            },
            matches = result.Matches.Select(m => new
            {
                type = m.Type.ToString(),
                original = m.OriginalValue,
                redacted = m.RedactedValue,
                confidence = m.Confidence,
                detector = m.DetectorName
            })
        });
    }

    /// <summary>
    /// Demonstrates exception handling with PII redaction.
    /// </summary>
    [HttpGet("test-exception")]
    [RedactPiiExceptions]
    public IActionResult TestException([FromQuery] string email = "user@example.com")
    {
        // This exception message contains PII that will be redacted
        throw new InvalidOperationException($"Failed to process user with email {email}");
    }

    /// <summary>
    /// Demonstrates GDPR-style full redaction.
    /// </summary>
    [HttpGet("gdpr-example")]
    public IActionResult GdprExample()
    {
        var userData = new
        {
            name = "John Smith",
            email = "john.smith@example.com",
            phone = "+44 20 7946 0958",
            address = "123 High Street, London",
            postcode = "SW1A 1AA",
            ni = "AB123456C",
            dob = "DOB: 15/03/1985"
        };

        var jsonText = System.Text.Json.JsonSerializer.Serialize(userData);
        var result = _redactionService.Redact(jsonText);

        return Ok(new
        {
            originalData = userData,
            originalJson = jsonText,
            redactedJson = result.RedactedText,
            piiFound = result.TypeCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        });
    }

    /// <summary>
    /// Demonstrates PCI-DSS style payment data redaction.
    /// </summary>
    [HttpGet("pci-example")]
    public IActionResult PciExample()
    {
        var paymentLog = @"
            Transaction ID: TXN-123456
            Card Number: 4111-1111-1111-1111
            Expiry: 12/25
            CVV: 123
            Amount: $99.99
            Customer Email: customer@example.com
        ";

        var result = _redactionService.Redact(paymentLog);

        return Ok(new
        {
            original = paymentLog,
            redacted = result.RedactedText,
            detectedItems = result.Matches.Select(m => new
            {
                type = m.Type.ToString(),
                redacted = m.RedactedValue
            })
        });
    }

    /// <summary>
    /// Returns current redaction statistics.
    /// </summary>
    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        var stats = _redactionService.GetStatistics();

        return Ok(new
        {
            totalScans = stats.TotalScans,
            totalRedactions = stats.TotalRedactions,
            totalCharactersScanned = stats.TotalCharactersScanned,
            totalCharactersRedacted = stats.TotalCharactersRedacted,
            redactionsByType = stats.RedactionsByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        });
    }
}

public record MultiPiiRequest(string Text);
