using Mostlylucid.Common.Configuration;

namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
///     Configuration options for PII redaction.
/// </summary>
public class PiiRedactionOptions : IServiceOptions
{
    /// <summary>
    ///     Types of PII to detect and redact.
    ///     Default is all types.
    /// </summary>
    public PiiType DetectionTypes { get; set; } = PiiType.All;

    /// <summary>
    ///     Default redaction style to use.
    /// </summary>
    public RedactionStyle DefaultStyle { get; set; } = RedactionStyle.FullMask;

    /// <summary>
    ///     Per-type redaction style overrides.
    /// </summary>
    public Dictionary<PiiType, RedactionStyle> StyleOverrides { get; set; } = new();

    /// <summary>
    ///     Character to use for masking (default: '*').
    /// </summary>
    public char MaskCharacter { get; set; } = '*';

    /// <summary>
    ///     Number of characters to show at start for partial masking.
    /// </summary>
    public int PartialMaskPrefixLength { get; set; } = 2;

    /// <summary>
    ///     Number of characters to show at end for partial masking.
    /// </summary>
    public int PartialMaskSuffixLength { get; set; } = 4;

    /// <summary>
    ///     Minimum confidence threshold for detection (0.0 to 1.0).
    /// </summary>
    public double MinConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    ///     Whether to enable case-insensitive pattern matching.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    ///     Custom patterns to detect as PII (regex patterns).
    /// </summary>
    public Dictionary<string, string> CustomPatterns { get; set; } = new();

    /// <summary>
    ///     Values to whitelist (never redact these values).
    /// </summary>
    public HashSet<string> Whitelist { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Domains to whitelist for email detection.
    /// </summary>
    public HashSet<string> WhitelistedEmailDomains { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether to log statistics about redactions.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    ///     Maximum text length to process (0 = no limit).
    /// </summary>
    public int MaxTextLength { get; set; } = 0;

    /// <summary>
    ///     Whether to enable parallel processing for large texts.
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    ///     Threshold for parallel processing (character count).
    /// </summary>
    public int ParallelThreshold { get; set; } = 10000;

    /// <summary>
    ///     Whether PII redaction is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}