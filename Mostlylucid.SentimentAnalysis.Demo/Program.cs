using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.SentimentAnalysis.Extensions;
using Mostlylucid.SentimentAnalysis.Services;

Console.WriteLine("Sentiment Analysis Demo");
Console.WriteLine("=======================\n");

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

if (!sentiment.IsReady)
{
    Console.WriteLine("Error: Sentiment analysis service failed to initialize.");
    return;
}

Console.WriteLine("Service initialized successfully!\n");

// Demo: Single text analysis
Console.WriteLine("=== Single Text Analysis ===\n");

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
    var result = await sentiment.AnalyzeAsync(text);
    Console.WriteLine($"Text: \"{text}\"");
    Console.WriteLine($"  Sentiment: {result.Sentiment}");
    Console.WriteLine($"  Confidence: {result.Confidence:P1}");
    Console.WriteLine($"  Score: {result.NormalizedScore:F2}");
    Console.WriteLine();
}

// Demo: Quick sentiment check
Console.WriteLine("=== Quick Sentiment Check ===\n");

var quickText = "The weather today is beautiful!";
var quickSentiment = await sentiment.GetSentimentAsync(quickText);
var quickScore = await sentiment.GetSentimentScoreAsync(quickText);

Console.WriteLine($"Text: \"{quickText}\"");
Console.WriteLine($"Sentiment: {quickSentiment}");
Console.WriteLine($"Score: {quickScore:F2}\n");

// Demo: Long text analysis
Console.WriteLine("=== Long Text Analysis ===\n");

var longText = @"
This review covers my experience with the new smartphone I purchased last month.

The initial setup was incredibly smooth. The device booted up quickly and the interface
was intuitive to navigate. I was impressed by how easy it was to transfer my data from
my old phone.

However, I did encounter some issues with the battery life. After about a week of use,
I noticed that the battery would drain much faster than advertised. This was particularly
frustrating during long days when I couldn't charge the phone.

The camera quality is outstanding though. Photos come out crisp and vibrant, even in
low light conditions. The video recording capabilities are equally impressive.

Customer support was helpful when I reached out about the battery issue. They provided
some tips that helped extend the battery life, though it still doesn't match the
advertised performance.

Overall, it's a mixed experience. Great hardware and features, but the battery issues
are a significant drawback. I would give it 3.5 out of 5 stars.
";

var longResult = await sentiment.AnalyzeLongTextAsync(longText, "Product Review");

Console.WriteLine($"Source: {longResult.Source}");
Console.WriteLine($"Overall Sentiment: {longResult.OverallSentiment}");
Console.WriteLine($"Average Confidence: {longResult.AverageConfidence:P1}");
Console.WriteLine($"Weighted Score: {longResult.WeightedScore:F2}");
Console.WriteLine($"Chunks Analyzed: {longResult.ChunkCount}");
Console.WriteLine("\nSentiment Distribution:");
foreach (var (label, count) in longResult.SentimentDistribution.OrderByDescending(x => x.Value))
{
    if (count > 0)
        Console.WriteLine($"  {label}: {count}");
}

Console.WriteLine("\n=== Demo Complete ===");
