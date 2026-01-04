# Mostlylucid.CFMoM

**Constrained Fuzzy Mixture of Models (CFMoM)** - A framework for reliable multi-proposer systems.

> LLMs interpret reality. They must never be allowed to define it.

## What is CFMoM?

CFMoM is a control pattern for systems where multiple probabilistic components (proposers) contribute evidence, but a deterministic component (constrainer) makes all final decisions. This pattern is essential for:

- Multi-agent AI systems
- Bot detection pipelines
- Document classification
- Fraud detection
- Any system where you need "wisdom of the crowd" from multiple imperfect sources

## Key Concepts

### 1. Signal Contract

Every proposer emits typed, immutable signals:

```csharp
public sealed record ConstrainedSignal
{
    string Id { get; }                      // Unique signal ID
    string SourceId { get; }                // Which proposer
    string FactsSchemaId { get; }           // Schema for validation
    JsonElement Facts { get; }              // The actual payload
    IReadOnlyList<EvidenceRef> Evidence { get; }  // Pointer-verifiable
    float Confidence { get; }               // 0.0 to 1.0
}
```

### 2. Consensus Space

A bus and memory, not an arbiter. Signals pass through the ingestion gate where schema validation occurs. The consensus space never makes decisions.

### 3. Wave-Based Orchestration

Proposers run in waves:
- Wave 0: Fast, cheap proposers (rule-based, cached)
- Wave 1: Medium proposers (triggered by Wave 0 signals)
- Wave 2+: Expensive proposers (LLM, external APIs)

### 4. Constrainer

The deterministic decision-maker. Contains NO probabilistic logic. All decisions are based on explicit thresholds and rules.

## Invariants

1. **Signals are immutable** after creation
2. **Facts validate** against their schema at ingestion
3. **Evidence is pointer-verifiable** (not necessarily verified at ingestion)
4. **Only the constrainer** can trigger side effects

## Quick Start

```csharp
// 1. Add services
services.AddCFMoMWithThresholds<HttpContext>(
    options => options.MaxWaves = 5,
    thresholds => thresholds.ImmediateBlockThreshold = 0.85);

// 2. Add proposers
services.AddCFMoMProposer<HttpContext, UserAgentProposer>();
services.AddCFMoMProposer<HttpContext, IpReputationProposer>();
services.AddCFMoMProposer<HttpContext, BehaviorProposer>();

// 3. Use in your code
public class MyService
{
    private readonly CFMoMOrchestrator<HttpContext, CommonDecision> _orchestrator;

    public async Task<CommonDecision> AnalyzeAsync(HttpContext context)
    {
        var result = await _orchestrator.ExecuteAsync(context);
        return result.Decision;
    }
}
```

## Creating a Proposer

```csharp
public class MyProposer : ProposerBase<HttpContext>
{
    public override string Name => "My";
    public override string FactsSchemaId => "my-analysis.v1";

    // Run after 2 proposers complete
    public override IReadOnlyList<TriggerCondition> TriggerConditions =>
        new[] { Triggers.WhenProposerCount(2) };

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<HttpContext> state,
        CancellationToken ct)
    {
        // Do analysis
        var confidence = AnalyzeSomething(state.Context);

        // Return signal
        return Single(CreateSignal(
            new { isBot = confidence > 0.5, reason = "pattern match" },
            confidence,
            evidence: new[] { EvidenceRef.Request("logs", state.CorrelationId) }
        ));
    }
}
```

## Evidence References

Signals can reference evidence that supports their claims:

```csharp
// Document chunks
EvidenceRef.Chunk("documents", "chunk-42", start: 0, end: 500);

// Video frames with bounding box
EvidenceRef.Frame("video", "frame-0042", x: 100, y: 200, w: 50, h: 50);

// Timestamps
EvidenceRef.Timestamp("audio", TimeSpan.FromSeconds(13.5));

// Database rows
EvidenceRef.Row("users", "user-123", hash: "abc123");
```

## Configuration

```csharp
services.AddCFMoMWithThresholds<MyContext>(
    options =>
    {
        options.TotalTimeout = TimeSpan.FromSeconds(10);
        options.MaxWaves = 5;
        options.MaxParallelProposers = 8;
        options.ParallelismPerWave = new() { [0] = 8, [2] = 2 }; // Low parallelism for LLM wave
        options.CircuitBreakerThreshold = 3;
    },
    thresholds =>
    {
        thresholds.ImmediateBlockThreshold = 0.85;
        thresholds.ImmediateAllowThreshold = 0.15;
        thresholds.ChallengeThreshold = 0.70;
    });
```

## Custom Constrainer

For domain-specific decisions:

```csharp
public class MyConstrainer : IConstrainer<MyContext, MyDecision>
{
    public ConstrainerResult<MyDecision> Evaluate(
        AggregatedResult result,
        MyContext context)
    {
        // All logic here is deterministic
        if (result.Score > 0.9)
            return ConstrainerResult<MyDecision>.Stop(
                MyDecision.Reject,
                "Score exceeds threshold");

        return ConstrainerResult<MyDecision>.Continue(
            MyDecision.Accept);
    }
}
```

## Integration with Mostlylucid.Ephemeral

CFMoM is built on [Mostlylucid.Ephemeral](https://github.com/scottgal/mostlylucid.nugetpackages) for signal coordination:

```csharp
services.AddSingleton<SignalSink>();

// Orchestrator will emit signals for observability
// - cfmom.started
// - wave.started
// - proposer.started
// - proposer.completed
// - cfmom.completed
```

## The Pattern in Action

See [The Ten Commandments of LLM Use](https://www.mostlylucid.net/blog/tencommandments) and [Constrained Fuzzy MoM: Signal Contracts Over Agent Chatter](https://www.mostlylucid.net/blog/constrained-mom-mixture-of-models) for the theory behind this pattern.

**Used in production** in [Mostlylucid.BotDetection](https://www.nuget.org/packages/Mostlylucid.BotDetection).

---

## CLI Demo: Prompt Router

The `Mostlylucid.CFMoM.Demo` project demonstrates a complete prompt router using:

- **Multi-tier LLM architecture** with Ollama
- **ONNX-based embeddings** (all-MiniLM-L6-v2, auto-downloaded)
- **Hybrid RAG learning** with RRF fusion

### Running the Demo

```bash
# With Ollama running locally
dotnet run --project Mostlylucid.CFMoM.Demo

# Or with a single prompt
dotnet run --project Mostlylucid.CFMoM.Demo -- "Write me a poem about the ocean"
```

### Multi-Tier LLM Architecture

The demo uses three tiers of models, each optimized for its task:

| Tier | Model | Purpose | Latency |
|------|-------|---------|---------|
| **Sentinel** | tinyllama (~1B) | Fast triage, early filtering | ~100ms |
| **Planners** | llama3.2:3b | Intent & sentiment analysis | ~500ms |
| **Evaluator** | llama3.1:8b | Deep safety evaluation | ~2-5s |

### Wave-Based Execution

```
Wave -1: Learning Cache (instant retrieval of known patterns)
Wave 0:  Sentinel + Intent + Sentiment (parallel, fast)
Wave 1:  Topic + Safety (conditional, triggered)
Wave 2+: Deep analysis (expensive, only if needed)
```

### Hybrid RAG Learning

The demo learns from decisions and reuses them for similar prompts:

**Three-Signal RRF Fusion:**
1. **Dense** - ONNX embedding cosine similarity
2. **BM25** - Lexical keyword matching
3. **Salience** - Confidence × log(hits+1)

```
RRF(d) = 1/(60 + rank_dense) + 1/(60 + rank_bm25) + 1/(60 + rank_salience)
```

### Example Output

```
╭─Multi-Tier LLM Architecture────╮
│ CFMoM Prompt Router Demo       │
│ Using Ollama: Connected        │
│ Learning DB: 5 entries, 12 hits│
╰────────────────────────────────╯

Processing: Write me a poem about the ocean

──────────────── Learning Check ────────────────
  No similar prompt found

──────────────── Waves (0) ────────────────
┌────────────────────┬────────┬───────────┐
│ Proposer           │ Status │ Signal    │
├────────────────────┼────────┼───────────┤
│ learning-cache     │ Done   │ -         │
│ intent-classifier  │ Done   │ conf: 94% │
│ sentiment-analyzer │ Done   │ conf: 82% │
│ sentinel-triage    │ Done   │ conf: 15% │
└────────────────────┴────────┴───────────┘

──────────────── Aggregation ────────────────
  Score:      15%
  Confidence: 88%
  Band:       Low

──────────── Decision: ALLOW ────────────
  Route:      CreativeWritingHandler
  Signals:    4
  Duration:   1240ms
  Decision saved to learning store
```

### ONNX Embedding Service

The demo uses local ONNX inference for embeddings:

- **Model**: all-MiniLM-L6-v2 (384 dimensions)
- **Size**: 23MB (quantized) / 90MB (full)
- **Auto-download**: First run downloads from HuggingFace
- **Cache**: `~/.cfmom/models/`

```csharp
// Automatic initialization with download
var embedding = embeddingService.Embed("Hello world");
// Returns float[384]
```

---

## Unit Tests

The `Mostlylucid.CFMoM.Tests` project provides comprehensive coverage:

```bash
dotnet test Mostlylucid.CFMoM.Tests
# 125 tests, 100% pass rate
```

### Test Coverage

| Component | Tests | Coverage |
|-----------|-------|----------|
| ConstrainedSignal | 14 | Immutability, metadata, evidence |
| ConsensusSpace | 19 | Ingestion, filtering, thread safety |
| WeightedAggregator | 14 | Scoring, confidence, bands |
| ThresholdConstrainer | 13 | All decision paths |
| TriggerConditions | 24 | All trigger types, combinators |
| ProposerBase | 15 | Signal creation, helpers |
| CFMoMOrchestrator | 13 | Waves, timeouts, circuit breaker |
| EvidenceRef | 6 | All reference types |
| Integration | 7 | End-to-end flows |

### Running Tests

```bash
# Run all tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific category
dotnet test --filter "FullyQualifiedName~ConsensusSpace"
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ORCHESTRATOR                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  Wave 0 (Parallel)                                          │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐                    │   │
│  │  │ Sentinel │ │  Intent  │ │Sentiment │                    │   │
│  │  │(tinyllama)│ │(llama3.2)│ │(llama3.2)│                    │   │
│  │  └────┬─────┘ └────┬─────┘ └────┬─────┘                    │   │
│  │       │            │            │                           │   │
│  │       └────────────┴────────────┘                           │   │
│  │                    │                                        │   │
│  │                    ▼                                        │   │
│  │  ┌─────────────────────────────────────────────────────┐   │   │
│  │  │           CONSENSUS SPACE                            │   │   │
│  │  │  - Signals collected with schema validation          │   │   │
│  │  │  - Evidence refs attached                            │   │   │
│  │  │  - Early exit detection                              │   │   │
│  │  └─────────────────────────────────────────────────────┘   │   │
│  │                    │                                        │   │
│  │                    ▼                                        │   │
│  │  ┌─────────────────────────────────────────────────────┐   │   │
│  │  │           AGGREGATOR                                 │   │   │
│  │  │  - Weighted score calculation                        │   │   │
│  │  │  - Confidence assessment                             │   │   │
│  │  │  - Classification band                               │   │   │
│  │  └─────────────────────────────────────────────────────┘   │   │
│  │                    │                                        │   │
│  │                    ▼                                        │   │
│  │  Wave 1 (Conditional)                                       │   │
│  │  ┌──────────┐ ┌──────────┐                                 │   │
│  │  │  Topic   │ │  Safety  │ ◄── Triggered by Wave 0 signals │   │
│  │  │(llama3.2)│ │(llama3.1)│                                 │   │
│  │  └────┬─────┘ └────┬─────┘                                 │   │
│  │       └────────────┘                                        │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                    │                                               │
│                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │           CONSTRAINER (Deterministic)                       │   │
│  │  - Threshold-based decisions                                │   │
│  │  - Allow / Block / Challenge / Escalate                     │   │
│  │  - Route to handler                                         │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Dependencies

### Library (Mostlylucid.CFMoM)
- .NET 9.0
- System.Text.Json

### Demo (Mostlylucid.CFMoM.Demo)
- Spectre.Console 0.49.1
- Microsoft.ML.OnnxRuntime 1.20.1
- DuckDB.NET.Data 1.1.3 (optional, falls back to in-memory)

### Tests (Mostlylucid.CFMoM.Tests)
- xUnit 2.9.2
- NSubstitute 5.3.0
- FluentAssertions 7.0.0

## License

Unlicense - do what you want.
