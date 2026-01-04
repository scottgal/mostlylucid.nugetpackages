namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     Fast keyword-based fact extraction (no LLM required).
///     Used for quick verification of learned decision matches.
/// </summary>
public static class QuickFactExtractor
{
    private static readonly Dictionary<string, string[]> IntentKeywords = new()
    {
        ["creative"] = ["write", "compose", "create", "generate", "poem", "story", "song", "describe", "imagine"],
        ["coding"] = ["code", "program", "debug", "fix", "implement", "function", "class", "algorithm", "compile", "error", "bug"],
        ["question"] = ["what", "how", "why", "explain", "tell me", "describe", "define", "mean"],
        ["command"] = ["do", "run", "execute", "delete", "remove", "install", "stop", "start", "open", "close", "kill"],
        ["chat"] = ["hello", "hi", "hey", "thanks", "bye", "help", "please"]
    };

    private static readonly HashSet<string> PositiveWords =
        ["good", "great", "love", "happy", "please", "thanks", "excellent", "amazing", "wonderful", "fantastic", "appreciate"];

    private static readonly HashSet<string> NegativeWords =
        ["bad", "hate", "angry", "kill", "destroy", "wrong", "terrible", "awful", "horrible", "worst", "fail"];

    private static readonly Dictionary<string, string[]> TopicKeywords = new()
    {
        ["technology"] = ["code", "programming", "computer", "software", "hardware", "api", "database", "server"],
        ["creative-writing"] = ["poem", "story", "novel", "write", "author", "fiction", "prose"],
        ["nature"] = ["ocean", "sea", "forest", "animal", "nature", "tree", "flower", "mountain", "river"],
        ["business"] = ["company", "market", "sales", "revenue", "business", "customer", "profit"],
        ["science"] = ["experiment", "research", "theory", "physics", "chemistry", "biology", "science"]
    };

    /// <summary>
    ///     Extract quick facts from a prompt for verification.
    /// </summary>
    public static Dictionary<string, string> Extract(string prompt)
    {
        var facts = new Dictionary<string, string>();
        var lowerPrompt = prompt.ToLowerInvariant();
        var words = lowerPrompt.Split([' ', '\t', '\n', '\r', '.', ',', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries);

        // Extract intent
        var intent = DetectIntent(words);
        facts["intent"] = intent;

        // Extract sentiment
        var sentiment = DetectSentiment(words);
        facts["sentiment"] = sentiment;

        // Extract topics
        var topics = DetectTopics(words);
        if (topics.Count > 0)
            facts["primary_topic"] = topics[0];

        // Detect question marker
        if (lowerPrompt.Contains('?'))
            facts["has_question"] = "true";

        // Detect command markers
        if (words.Any(w => w == "please" || w == "can" || w == "could" || w == "would"))
            facts["polite"] = "true";

        return facts;
    }

    /// <summary>
    ///     Compare extracted facts with stored facts.
    /// </summary>
    /// <param name="extractedFacts">Facts extracted from current prompt.</param>
    /// <param name="storedFacts">Facts stored with the learned decision.</param>
    /// <returns>Match score between 0 and 1.</returns>
    public static double CompareFactsMatch(
        Dictionary<string, string> extractedFacts,
        IEnumerable<LearnedFact> storedFacts)
    {
        var storedDict = storedFacts
            .GroupBy(f => f.FactKey)
            .ToDictionary(g => g.Key, g => g.First().FactValue);

        if (storedDict.Count == 0 || extractedFacts.Count == 0)
            return 0;

        var matchedKeys = 0;
        var totalWeight = 0.0;
        var matchedWeight = 0.0;

        // Key weights (some facts are more important than others)
        var keyWeights = new Dictionary<string, double>
        {
            ["intent"] = 3.0,
            ["sentiment"] = 2.0,
            ["primary_topic"] = 2.0,
            ["has_question"] = 1.0,
            ["polite"] = 0.5
        };

        foreach (var (key, extractedValue) in extractedFacts)
        {
            var weight = keyWeights.GetValueOrDefault(key, 1.0);
            totalWeight += weight;

            if (storedDict.TryGetValue(key, out var storedValue) &&
                string.Equals(extractedValue, storedValue, StringComparison.OrdinalIgnoreCase))
            {
                matchedWeight += weight;
                matchedKeys++;
            }
        }

        return totalWeight > 0 ? matchedWeight / totalWeight : 0;
    }

    private static string DetectIntent(string[] words)
    {
        foreach (var (intent, keywords) in IntentKeywords)
        {
            if (words.Any(w => keywords.Contains(w)))
                return intent;
        }

        return "general";
    }

    private static string DetectSentiment(string[] words)
    {
        var positiveCount = words.Count(w => PositiveWords.Contains(w));
        var negativeCount = words.Count(w => NegativeWords.Contains(w));

        if (positiveCount > negativeCount)
            return "positive";
        if (negativeCount > positiveCount)
            return "negative";
        return "neutral";
    }

    private static List<string> DetectTopics(string[] words)
    {
        var topics = new List<(string topic, int count)>();

        foreach (var (topic, keywords) in TopicKeywords)
        {
            var count = words.Count(w => keywords.Contains(w));
            if (count > 0)
                topics.Add((topic, count));
        }

        return topics
            .OrderByDescending(t => t.count)
            .Select(t => t.topic)
            .ToList();
    }
}
