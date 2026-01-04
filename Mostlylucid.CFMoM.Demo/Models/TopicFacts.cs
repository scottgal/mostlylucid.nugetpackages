namespace Mostlylucid.CFMoM.Demo.Models;

/// <summary>
///     Facts schema for topic classification signals.
/// </summary>
public sealed record TopicFacts
{
    /// <summary>
    ///     Schema ID for this facts type.
    /// </summary>
    public const string SchemaId = "topic.v1";

    /// <summary>
    ///     All detected topics.
    /// </summary>
    public required string[] Topics { get; init; }

    /// <summary>
    ///     The primary/main topic.
    /// </summary>
    public required string Primary { get; init; }

    /// <summary>
    ///     Relevance score for the primary topic (0-1).
    /// </summary>
    public double PrimaryRelevance { get; init; }

    /// <summary>
    ///     Whether the topic requires domain expertise.
    /// </summary>
    public bool RequiresExpertise { get; init; }
}
