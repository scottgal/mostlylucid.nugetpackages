using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Policies;

/// <summary>
///     Registry of named detection policies.
///     Provides lookup by name and path-based policy resolution.
/// </summary>
public interface IPolicyRegistry
{
    /// <summary>Get a policy by name</summary>
    DetectionPolicy? GetPolicy(string name);

    /// <summary>Get the policy for a given request path</summary>
    DetectionPolicy GetPolicyForPath(string path);

    /// <summary>Get all registered policies</summary>
    IReadOnlyDictionary<string, DetectionPolicy> GetAllPolicies();

    /// <summary>Register or update a policy</summary>
    void RegisterPolicy(DetectionPolicy policy);

    /// <summary>Remove a policy by name</summary>
    bool RemovePolicy(string name);

    /// <summary>Get the default policy</summary>
    DetectionPolicy DefaultPolicy { get; }
}

/// <summary>
///     In-memory implementation of the policy registry.
/// </summary>
public class PolicyRegistry : IPolicyRegistry
{
    private readonly ILogger<PolicyRegistry> _logger;
    private readonly ConcurrentDictionary<string, DetectionPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PathPolicyMapping> _pathMappings = [];
    private readonly object _pathLock = new();
    private DetectionPolicy _defaultPolicy;

    public PolicyRegistry(
        ILogger<PolicyRegistry> logger,
        IOptions<BotDetectionOptions> options)
    {
        _logger = logger;

        // Register built-in policies
        RegisterPolicy(DetectionPolicy.Default);
        RegisterPolicy(DetectionPolicy.Demo);
        RegisterPolicy(DetectionPolicy.Strict);
        RegisterPolicy(DetectionPolicy.Relaxed);
        RegisterPolicy(DetectionPolicy.Static);
        RegisterPolicy(DetectionPolicy.AllowVerifiedBots);
        RegisterPolicy(DetectionPolicy.Learning);
        RegisterPolicy(DetectionPolicy.Monitor);
        RegisterPolicy(DetectionPolicy.Api);
        RegisterPolicy(DetectionPolicy.FastWithOnnx);
        RegisterPolicy(DetectionPolicy.FastWithAi);

        // Load policies from configuration
        LoadFromOptions(options.Value);

        _defaultPolicy = _policies.GetValueOrDefault("default") ?? DetectionPolicy.Default;
    }

    public DetectionPolicy DefaultPolicy => _defaultPolicy;

    public DetectionPolicy? GetPolicy(string name)
    {
        return _policies.GetValueOrDefault(name);
    }

    public DetectionPolicy GetPolicyForPath(string path)
    {
        lock (_pathLock)
        {
            foreach (var mapping in _pathMappings)
            {
                if (mapping.Matches(path))
                {
                    if (_policies.TryGetValue(mapping.PolicyName, out var policy))
                    {
                        _logger.LogDebug("Path {Path} matched policy {Policy} via pattern {Pattern}",
                            path, policy.Name, mapping.PathPattern);
                        return policy;
                    }

                    _logger.LogWarning("Path {Path} matched pattern {Pattern} but policy {Policy} not found",
                        path, mapping.PathPattern, mapping.PolicyName);
                }
            }
        }

        return _defaultPolicy;
    }

    public IReadOnlyDictionary<string, DetectionPolicy> GetAllPolicies()
    {
        return _policies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public void RegisterPolicy(DetectionPolicy policy)
    {
        _policies[policy.Name] = policy;
        _logger.LogDebug("Registered policy {PolicyName}", policy.Name);

        if (policy.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            _defaultPolicy = policy;
        }
    }

    public bool RemovePolicy(string name)
    {
        if (name.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Cannot remove the default policy");
            return false;
        }

        var removed = _policies.TryRemove(name, out _);
        if (removed)
        {
            _logger.LogDebug("Removed policy {PolicyName}", name);
        }

        return removed;
    }

    private void LoadFromOptions(BotDetectionOptions options)
    {
        // Load custom policies
        foreach (var policyConfig in options.Policies)
        {
            var policy = CreatePolicyFromConfig(policyConfig.Key, policyConfig.Value);
            RegisterPolicy(policy);
        }

        // Load path mappings
        lock (_pathLock)
        {
            _pathMappings.Clear();

            // Add default static asset path mappings first (lowest priority)
            // User-defined mappings will override these due to higher specificity
            if (options.UseDefaultStaticPathPolicies)
            {
                AddDefaultStaticPathMappings();
            }

            // Add user-defined path mappings (these take precedence)
            foreach (var mapping in options.PathPolicies)
            {
                _pathMappings.Add(new PathPolicyMapping(mapping.Key, mapping.Value, isUserDefined: true));
                _logger.LogDebug("Registered path mapping: {Pattern} -> {Policy}",
                    mapping.Key, mapping.Value);
            }

            // Sort by priority: user-defined first, then by specificity (more specific patterns first)
            _pathMappings.Sort((a, b) =>
            {
                // User-defined mappings always win over defaults
                if (a.IsUserDefined != b.IsUserDefined)
                    return a.IsUserDefined ? -1 : 1;
                // Within same priority level, sort by specificity
                return b.Specificity.CompareTo(a.Specificity);
            });
        }

        // Set default policy if configured
        if (!string.IsNullOrEmpty(options.DefaultPolicyName) &&
            _policies.TryGetValue(options.DefaultPolicyName, out var defaultPolicy))
        {
            _defaultPolicy = defaultPolicy;
        }
    }

    /// <summary>
    ///     Adds default path mappings for static assets to use the "static" policy.
    ///     These are added at lowest priority so user-defined mappings take precedence.
    /// </summary>
    private void AddDefaultStaticPathMappings()
    {
        // Common static asset paths - map to "static" policy
        // The "static" policy uses FastPathReputation + UserAgent + Header only
        // (excludes Behavioral to avoid false positives from rapid parallel requests)
        var staticPaths = new[]
        {
            "/js/**",       // JavaScript bundles (webpack, etc.)
            "/css/**",      // CSS stylesheets
            "/lib/**",      // Library files (wwwroot/lib)
            "/fonts/**",    // Web fonts
            "/images/**",   // Image assets
            "/img/**",      // Image assets (alternate)
            "/assets/**",   // General assets folder
            "/static/**",   // Static files folder
            "/_content/**", // Blazor/Razor component content
            "/dist/**",     // Distribution/build output
            "/bundle/**",   // Bundled assets
            "/vendor/**"    // Vendor libraries
        };

        foreach (var path in staticPaths)
        {
            _pathMappings.Add(new PathPolicyMapping(path, "static"));
        }

        _logger.LogDebug(
            "Added {Count} default static path mappings to 'static' policy",
            staticPaths.Length);
    }

    private static DetectionPolicy CreatePolicyFromConfig(string name, DetectionPolicyConfig config)
    {
        // Skip disabled policies
        if (!config.Enabled)
        {
            return new DetectionPolicy
            {
                Name = name,
                Description = config.Description,
                Enabled = false
            };
        }

        // Use the ToPolicy() method from DetectionPolicyConfig
        return config.ToPolicy(name);
    }
}

/// <summary>
///     Maps a path pattern to a policy name.
/// </summary>
internal class PathPolicyMapping
{
    private readonly string _pattern;
    private readonly bool _isWildcard;
    private readonly bool _isPrefix;
    private readonly string _normalizedPattern;

    public PathPolicyMapping(string pattern, string policyName, bool isUserDefined = false)
    {
        _pattern = pattern;
        PolicyName = policyName;
        IsUserDefined = isUserDefined;

        // Normalize pattern
        _normalizedPattern = pattern.TrimEnd('/');

        // Detect pattern type
        _isWildcard = pattern.Contains('*');
        _isPrefix = pattern.EndsWith("/*") || pattern.EndsWith("/**");

        // Calculate specificity (more segments = more specific)
        Specificity = pattern.Count(c => c == '/');
        if (!_isWildcard) Specificity += 10; // Exact matches are most specific
    }

    public string PathPattern => _pattern;
    public string PolicyName { get; }
    public int Specificity { get; }
    public bool IsUserDefined { get; }

    public bool Matches(string path)
    {
        var normalizedPath = path.TrimEnd('/');

        // Exact match
        if (!_isWildcard)
        {
            return string.Equals(normalizedPath, _normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }

        // Prefix match (e.g., "/api/*" or "/api/**")
        if (_isPrefix)
        {
            var prefix = _normalizedPattern.TrimEnd('*', '/');
            return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // Simple glob matching
        return GlobMatch(normalizedPath, _normalizedPattern);
    }

    private static bool GlobMatch(string input, string pattern)
    {
        var inputIndex = 0;
        var patternIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (inputIndex < input.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToLowerInvariant(pattern[patternIndex]) == char.ToLowerInvariant(input[inputIndex])))
            {
                inputIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex;
                matchIndex = inputIndex;
                patternIndex++;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                matchIndex++;
                inputIndex = matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
