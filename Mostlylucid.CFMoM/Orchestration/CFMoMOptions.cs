namespace Mostlylucid.CFMoM.Orchestration;

/// <summary>
///     Configuration options for CFMoM orchestration.
/// </summary>
public sealed class CFMoMOptions
{
    /// <summary>
    ///     Maximum time for the entire orchestration pipeline.
    ///     Default: 5 seconds
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Maximum number of waves before stopping.
    ///     Prevents infinite loops from circular trigger dependencies.
    ///     Default: 10
    /// </summary>
    public int MaxWaves { get; set; } = 10;

    /// <summary>
    ///     Time to wait between waves for new triggers to become satisfied.
    ///     Default: 50ms
    /// </summary>
    public TimeSpan WaveInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///     Maximum parallel proposers per wave (global limit).
    ///     Set to 1 for fully sequential execution.
    ///     Default: 10
    /// </summary>
    public int MaxParallelProposers { get; set; } = 10;

    /// <summary>
    ///     Per-wave parallelism overrides.
    ///     Key: Wave number (0-based), Value: Max parallelism for that wave.
    ///     Example: { [0] = 8, [1] = 4, [2] = 2 } - high parallelism for fast proposers, low for LLM.
    ///     Default: empty (all waves use MaxParallelProposers)
    /// </summary>
    public Dictionary<int, int> ParallelismPerWave { get; set; } = new();

    /// <summary>
    ///     Circuit breaker: failures before disabling a proposer.
    ///     Default: 5
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    ///     Circuit breaker: time to wait before retrying a disabled proposer.
    ///     Default: 60 seconds
    /// </summary>
    public TimeSpan CircuitBreakerResetTime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Whether to enable quorum-based early exit.
    ///     When enabled, orchestration can exit early if enough proposers agree.
    ///     Default: true
    /// </summary>
    public bool EnableQuorumExit { get; set; } = true;

    /// <summary>
    ///     Minimum number of proposers that must complete before making a decision.
    ///     Prevents premature decisions from a single proposer.
    ///     Default: 3
    /// </summary>
    public int MinProposersForDecision { get; set; } = 3;
}
