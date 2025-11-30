using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.SentimentAnalysis.Extensions;
using Mostlylucid.SentimentAnalysis.Models;
using Mostlylucid.SentimentAnalysis.Services;

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("  Mostlylucid.SentimentAnalysis Demo");
Console.WriteLine("  CPU-only ONNX Sentiment Analysis");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();

// Build services
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddSentimentAnalysis(options =>
{
    options.ModelPath = "./models/sentiment";
    options.EnableDiagnosticLogging = true;
});

var serviceProvider = services.BuildServiceProvider();
var sentiment = serviceProvider.GetRequiredService<ISentimentAnalysisService>();

Console.WriteLine("Initializing sentiment analysis service...");
Console.WriteLine("(Model will be downloaded on first run - ~60MB)");
Console.WriteLine();

if (!sentiment.IsReady)
{
    Console.WriteLine("ERROR: Sentiment analysis service failed to initialize.");
    Console.WriteLine("Please check the logs above for details.");
    return 1;
}

Console.WriteLine("Service initialized successfully!\n");

// Run all demos
await RunSingleTextAnalysisDemo(sentiment);
await RunQuickSentimentDemo(sentiment);
await RunBatchAnalysisDemo(sentiment);
await RunMultilingualDemo(sentiment);
await RunLongTextAnalysisDemo(sentiment);
await RunStreamAnalysisDemo(sentiment);
await RunFileAnalysisDemo(sentiment);
await RunEdgeCasesDemo(sentiment);

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("  Demo Complete!");
Console.WriteLine("=".PadRight(60, '='));

return 0;

// ============================================================================
// Demo Methods
// ============================================================================

async Task RunSingleTextAnalysisDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Single Text Analysis");

    var testTexts = new[]
    {
        "I absolutely love this product! It exceeded all my expectations.",
        "This is the worst experience I've ever had. Terrible service.",
        "It's okay, nothing special but does the job.",
        "Pretty good overall, would recommend to friends.",
        "Completely disappointed. Waste of money and time."
    };

    foreach (var text in testTexts)
    {
        var result = await svc.AnalyzeAsync(text);
        PrintSentimentResult(text, result);
    }

    Console.WriteLine();
}

async Task RunQuickSentimentDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Quick Sentiment Check");

    var texts = new[]
    {
        "The weather today is beautiful!",
        "I hate waiting in long lines.",
        "The meeting was productive."
    };

    Console.WriteLine("Using GetSentimentAsync() and GetSentimentScoreAsync() for quick checks:\n");

    foreach (var text in texts)
    {
        var label = await svc.GetSentimentAsync(text);
        var score = await svc.GetSentimentScoreAsync(text);
        var emoji = GetSentimentEmoji(label);

        Console.WriteLine($"  \"{text}\"");
        Console.WriteLine($"    {emoji} {label} (score: {score:F2})");
        Console.WriteLine();
    }
}

async Task RunBatchAnalysisDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Batch Analysis");

    var reviews = new[]
    {
        "Great product! Works exactly as described.",
        "Shipping was fast and packaging was excellent.",
        "Not worth the price. Very disappointed.",
        "Average quality, nothing special.",
        "Absolutely fantastic! Best purchase I've made.",
        "Terrible customer service experience.",
        "The product is okay but could be better.",
        "Love it! Highly recommend to everyone."
    };

    Console.WriteLine($"Analyzing {reviews.Length} reviews in batch...\n");

    var results = await svc.AnalyzeBatchAsync(reviews);

    // Summary statistics
    var positive = results.Count(r => r.IsPositive);
    var negative = results.Count(r => r.IsNegative);
    var neutral = results.Count(r => r.IsNeutral);
    var avgScore = results.Average(r => r.NormalizedScore);

    Console.WriteLine("Results:");
    Console.WriteLine("-".PadRight(50, '-'));

    foreach (var result in results)
    {
        var emoji = GetSentimentEmoji(result.Sentiment);
        var bar = GetScoreBar(result.NormalizedScore);
        Console.WriteLine($"  {emoji} [{bar}] {result.Sentiment,-12} | {result.Text}");
    }

    Console.WriteLine("-".PadRight(50, '-'));
    Console.WriteLine($"\nSummary:");
    Console.WriteLine($"  Positive: {positive} ({positive * 100.0 / reviews.Length:F0}%)");
    Console.WriteLine($"  Neutral:  {neutral} ({neutral * 100.0 / reviews.Length:F0}%)");
    Console.WriteLine($"  Negative: {negative} ({negative * 100.0 / reviews.Length:F0}%)");
    Console.WriteLine($"  Average Score: {avgScore:F2} ({(avgScore >= 0 ? "positive" : "negative")} overall)");
    Console.WriteLine();
}

async Task RunMultilingualDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Multilingual Support");

    Console.WriteLine("The model supports 104 languages. Here are some examples:\n");

    var multilingualTexts = new (string Language, string Text)[]
    {
        ("English", "I love this product! It's amazing!"),
        ("Spanish", "Me encanta este producto! Es increible!"),
        ("French", "J'adore ce produit! C'est magnifique!"),
        ("German", "Ich liebe dieses Produkt! Es ist wunderbar!"),
        ("Italian", "Adoro questo prodotto! E' fantastico!"),
        ("Portuguese", "Eu amo este produto! E maravilhoso!"),
        ("Dutch", "Ik hou van dit product! Het is geweldig!"),
        ("Russian", "Я люблю этот продукт! Он замечательный!"),
        ("Chinese", "我喜欢这个产品！太棒了！"),
        ("Japanese", "この製品が大好きです！素晴らしいです！"),
        ("Korean", "이 제품을 정말 좋아해요! 훌륭해요!"),
        ("Arabic", "أنا أحب هذا المنتج! إنه رائع!")
    };

    foreach (var (language, text) in multilingualTexts)
    {
        var result = await svc.AnalyzeAsync(text);
        var emoji = GetSentimentEmoji(result.Sentiment);
        Console.WriteLine($"  [{language,-10}] {emoji} {result.Sentiment,-12} ({result.Confidence:P0}) \"{text}\"");
    }

    Console.WriteLine();
}

async Task RunLongTextAnalysisDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Long Text Analysis (Auto-Chunking)");

    var longText = @"
This comprehensive review covers my three-month experience with the new laptop I purchased.

FIRST IMPRESSIONS (Positive):
The unboxing experience was delightful. Premium packaging, included accessories, and the laptop itself
looked absolutely stunning. The build quality exceeded my expectations - solid aluminum chassis with
minimal flex. Setup was incredibly smooth, taking less than 15 minutes to get everything configured.

PERFORMANCE (Very Positive):
The processor handles everything I throw at it with ease. Video editing, compiling code, running
multiple virtual machines - nothing slows this machine down. The SSD is blazingly fast with boot
times under 10 seconds. Gaming performance is surprisingly good for an ultrabook.

DISPLAY (Positive):
The 4K display is gorgeous with accurate colors and excellent brightness. HDR content looks
fantastic. The anti-glare coating works well in bright environments.

BATTERY LIFE (Negative):
This is where my experience sours. The advertised 12-hour battery life is a fantasy. In real-world
usage with moderate brightness, I get maybe 5-6 hours. Heavy workloads drain it in under 3 hours.
This is extremely disappointing and has significantly impacted my workflow.

KEYBOARD AND TRACKPAD (Neutral):
The keyboard is decent but not exceptional. Key travel is shallow, which takes getting used to.
The trackpad is large and responsive, though I've had occasional palm rejection issues.

CUSTOMER SUPPORT (Very Negative):
When I contacted support about the battery issue, the experience was frustrating. Long wait times,
unhelpful responses, and ultimately no resolution. They blamed my usage patterns rather than
acknowledging the discrepancy with advertised specs.

FINAL VERDICT:
Despite the excellent performance and display, the battery life issues and poor customer support
significantly detract from the overall experience. I would rate this laptop 3 out of 5 stars.
It's a capable machine, but the negatives are hard to overlook, especially at this price point.
";

    Console.WriteLine("Analyzing a detailed product review with multiple sections...\n");

    var result = await svc.AnalyzeLongTextAsync(longText, "Product Review");

    Console.WriteLine($"Source: {result.Source}");
    Console.WriteLine($"Chunks Analyzed: {result.ChunkCount}");
    Console.WriteLine();
    Console.WriteLine($"Overall Sentiment: {GetSentimentEmoji(result.OverallSentiment)} {result.OverallSentiment}");
    Console.WriteLine($"Weighted Score: {result.WeightedScore:F2} ({GetScoreDescription(result.WeightedScore)})");
    Console.WriteLine($"Average Confidence: {result.AverageConfidence:P1}");
    Console.WriteLine();
    Console.WriteLine("Sentiment Distribution Across Chunks:");
    foreach (var (label, count) in result.SentimentDistribution.OrderByDescending(x => x.Value))
    {
        if (count > 0)
        {
            var bar = new string('#', count * 3);
            Console.WriteLine($"  {label,-14} {bar} ({count})");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Individual Chunk Results:");
    Console.WriteLine("-".PadRight(50, '-'));
    for (int i = 0; i < result.ChunkResults.Count; i++)
    {
        var chunk = result.ChunkResults[i];
        var emoji = GetSentimentEmoji(chunk.Sentiment);
        Console.WriteLine($"  Chunk {i + 1}: {emoji} {chunk.Sentiment,-12} ({chunk.Confidence:P0})");
    }
    Console.WriteLine();
}

async Task RunStreamAnalysisDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Stream Analysis");

    var text = @"
Customer feedback from our latest survey has been overwhelmingly positive.
Users love the new interface design and the improved performance.
However, some customers have expressed concerns about the pricing changes.
Overall, the sentiment towards our product remains strong and favorable.
";

    Console.WriteLine("Analyzing text from a MemoryStream...\n");

    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
    var result = await svc.AnalyzeStreamAsync(stream, "Survey Feedback");

    Console.WriteLine($"Source: {result.Source}");
    Console.WriteLine($"Overall: {GetSentimentEmoji(result.OverallSentiment)} {result.OverallSentiment}");
    Console.WriteLine($"Score: {result.WeightedScore:F2}");
    Console.WriteLine($"Chunks: {result.ChunkCount}");
    Console.WriteLine();
}

async Task RunFileAnalysisDemo(ISentimentAnalysisService svc)
{
    PrintHeader("File Analysis");

    // Create a temporary test file
    var tempFile = Path.Combine(Path.GetTempPath(), "sentiment_test.txt");

    var fileContent = @"
Email Thread: Project Status Update

From: John
Subject: Great news about the launch!

Team, I'm thrilled to announce that our product launch was a massive success!
The numbers exceeded our projections by 40%. Customer feedback has been
incredibly positive, with many praising the intuitive design and reliability.

---

From: Sarah
RE: Great news about the launch!

This is wonderful news! The team worked so hard on this, and it's fantastic
to see our efforts paying off. I'm especially proud of how we handled the
last-minute challenges.

---

From: Mike
RE: Great news about the launch!

While I'm glad about the overall success, I need to flag some concerns.
Our support team is overwhelmed with tickets. Response times have increased
significantly, and customer satisfaction scores are starting to drop.
We need to address this urgently before it affects our reputation.

---

From: Lisa
RE: Great news about the launch!

Mike raises valid points. We should schedule a meeting to discuss
resource allocation for support. That said, the product feedback is
genuinely excellent - customers love the new features.
";

    try
    {
        await File.WriteAllTextAsync(tempFile, fileContent);
        Console.WriteLine($"Created test file: {tempFile}\n");
        Console.WriteLine("Analyzing file content (email thread)...\n");

        var result = await svc.AnalyzeFileAsync(tempFile);

        Console.WriteLine($"File: {Path.GetFileName(result.Source)}");
        Console.WriteLine($"Overall: {GetSentimentEmoji(result.OverallSentiment)} {result.OverallSentiment}");
        Console.WriteLine($"Weighted Score: {result.WeightedScore:F2}");
        Console.WriteLine($"Average Confidence: {result.AverageConfidence:P1}");
        Console.WriteLine($"Chunks Analyzed: {result.ChunkCount}");
        Console.WriteLine();

        Console.WriteLine("Sentiment Distribution:");
        foreach (var (label, count) in result.SentimentDistribution.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"  {label}: {count}");
        }
    }
    finally
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
            Console.WriteLine($"\nCleaned up test file.");
        }
    }
    Console.WriteLine();
}

async Task RunEdgeCasesDemo(ISentimentAnalysisService svc)
{
    PrintHeader("Edge Cases and Special Inputs");

    var edgeCases = new (string Description, string Text)[]
    {
        ("Very short text", "Good!"),
        ("Single word", "Excellent"),
        ("Mixed sentiment", "Great product but terrible shipping"),
        ("Sarcasm (tricky)", "Oh great, another delay. Just what I needed."),
        ("Emoji-heavy", "Love this!!! So happy!!!"),
        ("Technical/neutral", "The function returns a boolean value."),
        ("Questions", "Is this the best product available?"),
        ("Numbers and data", "Sales increased by 50% this quarter.")
    };

    foreach (var (description, text) in edgeCases)
    {
        var result = await svc.AnalyzeAsync(text);
        var emoji = GetSentimentEmoji(result.Sentiment);
        Console.WriteLine($"  [{description,-20}]");
        Console.WriteLine($"    Input: \"{text}\"");
        Console.WriteLine($"    Result: {emoji} {result.Sentiment} ({result.Confidence:P0})");
        Console.WriteLine();
    }
}

// ============================================================================
// Helper Methods
// ============================================================================

void PrintHeader(string title)
{
    Console.WriteLine();
    Console.WriteLine($">>> {title}");
    Console.WriteLine("-".PadRight(50, '-'));
    Console.WriteLine();
}

void PrintSentimentResult(string text, SentimentResult result)
{
    var emoji = GetSentimentEmoji(result.Sentiment);
    var truncatedText = text.Length > 60 ? text[..57] + "..." : text;

    Console.WriteLine($"  \"{truncatedText}\"");
    Console.WriteLine($"    {emoji} Sentiment: {result.Sentiment}");
    Console.WriteLine($"       Confidence: {result.Confidence:P1}");
    Console.WriteLine($"       Score: {result.NormalizedScore:F2} ({GetScoreDescription(result.NormalizedScore)})");
    Console.WriteLine();
}

string GetSentimentEmoji(SentimentLabel sentiment) => sentiment switch
{
    SentimentLabel.VeryPositive => "[++]",
    SentimentLabel.Positive => "[+ ]",
    SentimentLabel.Neutral => "[~ ]",
    SentimentLabel.Negative => "[- ]",
    SentimentLabel.VeryNegative => "[--]",
    _ => "[? ]"
};

string GetScoreDescription(float score) => score switch
{
    >= 0.5f => "very positive",
    >= 0.1f => "positive",
    > -0.1f => "neutral",
    > -0.5f => "negative",
    _ => "very negative"
};

string GetScoreBar(float score)
{
    // Create a visual bar from -1 to +1
    // [-====|    ] for -0.5
    // [    |====] for +0.5
    var position = (int)((score + 1) / 2 * 10); // 0-10
    position = Math.Clamp(position, 0, 10);

    var bar = new char[11];
    for (int i = 0; i < 11; i++)
    {
        if (i == 5) bar[i] = '|';
        else if (i < 5 && i >= position) bar[i] = '=';
        else if (i > 5 && i <= position) bar[i] = '=';
        else bar[i] = ' ';
    }

    return new string(bar);
}
