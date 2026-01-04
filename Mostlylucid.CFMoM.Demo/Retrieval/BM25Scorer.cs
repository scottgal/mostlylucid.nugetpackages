namespace Mostlylucid.CFMoM.Demo.Retrieval;

/// <summary>
///     BM25 scorer for lexical/keyword matching.
///     Based on Okapi BM25 with standard parameters.
/// </summary>
public sealed class BM25Scorer
{
    private const double K1 = 1.5;  // Term frequency saturation
    private const double B = 0.75;  // Length normalization

    private readonly Dictionary<string, int> _docFreq = new();
    private double _avgDocLength;
    private int _totalDocs;

    /// <summary>
    ///     Initialize the scorer with a corpus of documents.
    /// </summary>
    public void Initialize(IEnumerable<(string id, string text)> documents)
    {
        var docs = documents.ToList();
        _totalDocs = docs.Count;
        _docFreq.Clear();

        var totalLength = 0.0;

        foreach (var (_, text) in docs)
        {
            var terms = Tokenize(text);
            totalLength += terms.Length;

            foreach (var term in terms.Distinct())
            {
                if (!_docFreq.TryGetValue(term, out var count))
                    _docFreq[term] = 0;
                _docFreq[term] = count + 1;
            }
        }

        _avgDocLength = _totalDocs > 0 ? totalLength / _totalDocs : 0;
    }

    /// <summary>
    ///     Score a document against a query.
    /// </summary>
    public double Score(string query, string document)
    {
        if (_totalDocs == 0 || string.IsNullOrWhiteSpace(document))
            return 0;

        var queryTerms = Tokenize(query);
        var docTerms = Tokenize(document);
        var docLength = docTerms.Length;

        if (docLength == 0)
            return 0;

        // Count term frequencies in document
        var termFreq = docTerms
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        var score = 0.0;

        foreach (var term in queryTerms.Distinct())
        {
            if (!termFreq.TryGetValue(term, out var tf))
                continue;

            // IDF calculation
            var df = _docFreq.GetValueOrDefault(term, 0);
            var idf = Math.Log((_totalDocs - df + 0.5) / (df + 0.5) + 1);

            // BM25 term score
            var numerator = tf * (K1 + 1);
            var denominator = tf + K1 * (1 - B + B * docLength / _avgDocLength);
            var termScore = idf * numerator / denominator;

            score += termScore;
        }

        return score;
    }

    /// <summary>
    ///     Tokenize text into terms.
    ///     No stopword filtering - IDF naturally downweights high-frequency terms.
    /// </summary>
    public static string[] Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', ':', ';', '"', '\'', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .ToArray();
    }
}
