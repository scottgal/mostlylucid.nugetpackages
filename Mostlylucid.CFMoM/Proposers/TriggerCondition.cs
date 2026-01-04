namespace Mostlylucid.CFMoM.Proposers;

/// <summary>
///     Condition that must be satisfied for a proposer to run.
///     Used for wave-based orchestration.
/// </summary>
public abstract record TriggerCondition
{
    /// <summary>
    ///     Human-readable description of this condition.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    ///     Check if this condition is satisfied given current orchestration state.
    /// </summary>
    public abstract bool IsSatisfied(IReadOnlyDictionary<string, object> signals);
}

/// <summary>
///     Trigger when a specific signal key exists.
/// </summary>
public sealed record SignalExistsTrigger(string SignalKey) : TriggerCondition
{
    public override string Description => $"Signal '{SignalKey}' exists";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return signals.ContainsKey(SignalKey);
    }
}

/// <summary>
///     Trigger when a signal has a specific value.
/// </summary>
public sealed record SignalValueTrigger<T>(string SignalKey, T ExpectedValue) : TriggerCondition
{
    public override string Description => $"Signal '{SignalKey}' == {ExpectedValue}";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return signals.TryGetValue(SignalKey, out var value) &&
               value is T typed &&
               EqualityComparer<T>.Default.Equals(typed, ExpectedValue);
    }
}

/// <summary>
///     Trigger when a signal satisfies a predicate.
/// </summary>
public sealed record SignalPredicateTrigger<T>(
    string SignalKey,
    Func<T, bool> Predicate,
    string PredicateDescription) : TriggerCondition
{
    public override string Description => $"Signal '{SignalKey}' {PredicateDescription}";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return signals.TryGetValue(SignalKey, out var value) &&
               value is T typed &&
               Predicate(typed);
    }
}

/// <summary>
///     Trigger when any of the sub-conditions are met (OR).
/// </summary>
public sealed record AnyOfTrigger(IReadOnlyList<TriggerCondition> Conditions) : TriggerCondition
{
    public override string Description => $"Any of: [{string.Join(", ", Conditions.Select(c => c.Description))}]";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return Conditions.Any(c => c.IsSatisfied(signals));
    }
}

/// <summary>
///     Trigger when all of the sub-conditions are met (AND).
/// </summary>
public sealed record AllOfTrigger(IReadOnlyList<TriggerCondition> Conditions) : TriggerCondition
{
    public override string Description => $"All of: [{string.Join(", ", Conditions.Select(c => c.Description))}]";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return Conditions.All(c => c.IsSatisfied(signals));
    }
}

/// <summary>
///     Trigger when a certain number of proposers have completed.
/// </summary>
public sealed record ProposerCountTrigger(int MinProposers) : TriggerCondition
{
    public const string CompletedProposersSignal = "_system.completed_proposers";

    public override string Description => $"At least {MinProposers} proposers completed";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return signals.TryGetValue(CompletedProposersSignal, out var value) &&
               value is int count &&
               count >= MinProposers;
    }
}

/// <summary>
///     Trigger when current aggregated score exceeds a threshold.
/// </summary>
public sealed record ScoreThresholdTrigger(double MinScore) : TriggerCondition
{
    public const string CurrentScoreSignal = "_system.current_score";

    public override string Description => $"Score >= {MinScore:F2}";

    public override bool IsSatisfied(IReadOnlyDictionary<string, object> signals)
    {
        return signals.TryGetValue(CurrentScoreSignal, out var value) &&
               value is double score &&
               score >= MinScore;
    }
}

/// <summary>
///     Helper class for building trigger conditions fluently.
/// </summary>
public static class Triggers
{
    /// <summary>
    ///     No trigger conditions (runs in wave 0).
    /// </summary>
    public static IReadOnlyList<TriggerCondition> None => Array.Empty<TriggerCondition>();

    /// <summary>
    ///     Trigger when a signal exists.
    /// </summary>
    public static TriggerCondition WhenSignalExists(string signalKey)
        => new SignalExistsTrigger(signalKey);

    /// <summary>
    ///     Trigger when a signal has a specific value.
    /// </summary>
    public static TriggerCondition WhenSignalEquals<T>(string signalKey, T value)
        => new SignalValueTrigger<T>(signalKey, value);

    /// <summary>
    ///     Trigger when a signal satisfies a predicate.
    /// </summary>
    public static TriggerCondition WhenSignal<T>(string signalKey, Func<T, bool> predicate, string description)
        => new SignalPredicateTrigger<T>(signalKey, predicate, description);

    /// <summary>
    ///     Trigger when any condition is met (OR).
    /// </summary>
    public static TriggerCondition AnyOf(params TriggerCondition[] conditions)
        => new AnyOfTrigger(conditions);

    /// <summary>
    ///     Trigger when all conditions are met (AND).
    /// </summary>
    public static TriggerCondition AllOf(params TriggerCondition[] conditions)
        => new AllOfTrigger(conditions);

    /// <summary>
    ///     Trigger when enough proposers have completed.
    /// </summary>
    public static TriggerCondition WhenProposerCount(int min)
        => new ProposerCountTrigger(min);

    /// <summary>
    ///     Trigger when score exceeds threshold.
    /// </summary>
    public static TriggerCondition WhenScoreExceeds(double threshold)
        => new ScoreThresholdTrigger(threshold);
}
