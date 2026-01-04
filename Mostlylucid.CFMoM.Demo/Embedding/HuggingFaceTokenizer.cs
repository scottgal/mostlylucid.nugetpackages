using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mostlylucid.CFMoM.Demo.Embedding;

/// <summary>
///     Unified tokenizer that supports WordPiece tokenization
///     by parsing HuggingFace's tokenizer.json format.
/// </summary>
public partial class HuggingFaceTokenizer
{
    private readonly TokenizerConfig _config;
    private readonly Dictionary<string, int> _vocab;
    private readonly string _continuingSubwordPrefix;
    private readonly string _unkToken;
    private readonly int _maxInputCharsPerWord;

    // Special token IDs
    public int ClsTokenId { get; }
    public int SepTokenId { get; }
    public int PadTokenId { get; }
    public int UnkTokenId { get; }

    private HuggingFaceTokenizer(
        TokenizerConfig config,
        Dictionary<string, int> vocab,
        string continuingSubwordPrefix,
        string unkToken,
        int maxInputCharsPerWord)
    {
        _config = config;
        _vocab = vocab;
        _continuingSubwordPrefix = continuingSubwordPrefix;
        _unkToken = unkToken;
        _maxInputCharsPerWord = maxInputCharsPerWord;

        // Resolve special tokens
        ClsTokenId = ResolveSpecialToken("[CLS]", 101);
        SepTokenId = ResolveSpecialToken("[SEP]", 102);
        PadTokenId = ResolveSpecialToken("[PAD]", 0);
        UnkTokenId = ResolveSpecialToken("[UNK]", 100);
    }

    /// <summary>
    ///     Load tokenizer from tokenizer.json file.
    /// </summary>
    public static HuggingFaceTokenizer FromFile(string tokenizerJsonPath)
    {
        var json = File.ReadAllText(tokenizerJsonPath);
        return FromJson(json);
    }

    /// <summary>
    ///     Load tokenizer from JSON string.
    /// </summary>
    public static HuggingFaceTokenizer FromJson(string json)
    {
        var config = JsonSerializer.Deserialize<TokenizerConfig>(json, JsonOptions)
                     ?? throw new InvalidOperationException("Failed to parse tokenizer.json");

        // Build vocabulary
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        if (config.Model?.Vocab != null)
        {
            foreach (var kvp in config.Model.Vocab)
            {
                vocab[kvp.Key] = kvp.Value;
            }
        }

        var prefix = config.Model?.ContinuingSubwordPrefix ?? "##";
        var unkToken = config.Model?.UnkToken ?? "[UNK]";
        var maxChars = config.Model?.MaxInputCharsPerWord ?? 100;

        return new HuggingFaceTokenizer(config, vocab, prefix, unkToken, maxChars);
    }

    /// <summary>
    ///     Create a fallback tokenizer from vocab.txt (legacy WordPiece format).
    /// </summary>
    public static HuggingFaceTokenizer FromVocabFile(string vocabPath)
    {
        var vocab = File.ReadAllLines(vocabPath)
            .Select((word, index) => (word, index))
            .ToDictionary(x => x.word, x => x.index, StringComparer.Ordinal);

        var config = new TokenizerConfig();
        return new HuggingFaceTokenizer(config, vocab, "##", "[UNK]", 100);
    }

    /// <summary>
    ///     Encode text to token IDs with attention mask.
    /// </summary>
    public (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Encode(string text, int maxLength)
    {
        // Normalize - lowercase and clean whitespace
        var normalized = NormalizeText(text);

        // Pre-tokenize - split on whitespace and punctuation
        var preTokens = PreTokenize(normalized);

        // Tokenize each pre-token using WordPiece
        var tokens = new List<string>();
        foreach (var preToken in preTokens)
        {
            tokens.AddRange(TokenizeWordPiece(preToken));
        }

        // Truncate to fit special tokens
        if (tokens.Count > maxLength - 2)
            tokens = tokens.Take(maxLength - 2).ToList();

        // Build input IDs with special tokens
        var inputIds = new List<long> { ClsTokenId };
        inputIds.AddRange(tokens.Select(t => (long)GetTokenId(t)));
        inputIds.Add(SepTokenId);

        // Pad to maxLength
        var padCount = maxLength - inputIds.Count;
        inputIds.AddRange(Enumerable.Repeat((long)PadTokenId, padCount));

        var attentionMask = inputIds.Select(id => id != PadTokenId ? 1L : 0L).ToArray();
        var tokenTypeIds = new long[maxLength]; // All zeros for single sentence

        return (inputIds.ToArray(), attentionMask, tokenTypeIds);
    }

    private static string NormalizeText(string text)
    {
        // Clean whitespace
        text = WhitespaceRegex().Replace(text, " ").Trim();

        // Handle Chinese characters (add spaces around them)
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (IsChineseChar(c))
            {
                sb.Append(' ').Append(c).Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }
        text = sb.ToString();

        return text.ToLowerInvariant();
    }

    private static IEnumerable<string> PreTokenize(string text)
    {
        // Split on whitespace
        var words = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            // Split on punctuation while keeping it
            var parts = PunctuationRegex().Split(word);
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                    yield return part;
            }
        }
    }

    private IEnumerable<string> TokenizeWordPiece(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        // Check if whole word is in vocab
        if (_vocab.ContainsKey(text))
        {
            yield return text;
            yield break;
        }

        // Too long - return UNK
        if (text.Length > _maxInputCharsPerWord)
        {
            yield return _unkToken;
            yield break;
        }

        // WordPiece greedy longest-match-first
        int start = 0;
        while (start < text.Length)
        {
            int end = text.Length;
            string? curSubstr = null;

            while (start < end)
            {
                var substr = text[start..end];
                if (start > 0)
                    substr = _continuingSubwordPrefix + substr;

                if (_vocab.ContainsKey(substr))
                {
                    curSubstr = substr;
                    break;
                }
                end--;
            }

            if (curSubstr == null)
            {
                yield return _unkToken;
                yield break;
            }

            yield return curSubstr;
            start = end;
        }
    }

    private int GetTokenId(string token) =>
        _vocab.GetValueOrDefault(token, UnkTokenId);

    private int ResolveSpecialToken(string defaultToken, int defaultId)
    {
        // Try to find in added_tokens
        var addedToken = _config.AddedTokens?.FirstOrDefault(t => t.Content == defaultToken);

        if (addedToken != null && addedToken.Id.HasValue)
            return addedToken.Id.Value;

        // Try vocabulary
        if (_vocab.TryGetValue(defaultToken, out var vocabId))
            return vocabId;

        return defaultId;
    }

    private static bool IsChineseChar(char c)
    {
        // CJK Unified Ideographs and related blocks
        return (c >= 0x4E00 && c <= 0x9FFF) ||
               (c >= 0x3400 && c <= 0x4DBF) ||
               (c >= 0xF900 && c <= 0xFAFF);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"([^\w\s])")]
    private static partial Regex PunctuationRegex();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

#region JSON Config Classes

internal class TokenizerConfig
{
    [JsonPropertyName("model")]
    public ModelConfig? Model { get; set; }

    [JsonPropertyName("added_tokens")]
    public List<AddedToken>? AddedTokens { get; set; }
}

internal class ModelConfig
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("vocab")]
    public Dictionary<string, int>? Vocab { get; set; }

    [JsonPropertyName("unk_token")]
    public string? UnkToken { get; set; }

    [JsonPropertyName("continuing_subword_prefix")]
    public string? ContinuingSubwordPrefix { get; set; }

    [JsonPropertyName("max_input_chars_per_word")]
    public int? MaxInputCharsPerWord { get; set; }
}

internal class AddedToken
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

#endregion
