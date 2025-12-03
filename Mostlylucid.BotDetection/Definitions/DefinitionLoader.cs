using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.BotDetection.Definitions;

/// <summary>
///     Loads and resolves policy definitions from embedded JSON resources.
///     Handles inheritance (Extends) and logs the resolution chain.
/// </summary>
public class DefinitionLoader
{
    private readonly ILogger<DefinitionLoader>? _logger;
    private readonly Dictionary<string, JsonElement> _rawDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResolvedDefinition> _resolved = new(StringComparer.OrdinalIgnoreCase);

    public DefinitionLoader(ILogger<DefinitionLoader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Loads all action policy definitions from embedded resources.
    /// </summary>
    public Dictionary<string, ResolvedDefinition> LoadActionPolicies()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("Definitions.Actions") && n.EndsWith(".json") && !n.EndsWith(".schema.json"));

        foreach (var resourceName in resourceNames)
        {
            LoadDefinitionsFromResource(assembly, resourceName);
        }

        // Resolve inheritance for all definitions
        ResolveAllInheritance();

        _logger?.LogInformation(
            "Loaded {Count} action policy definitions from embedded resources",
            _resolved.Count);

        return _resolved;
    }

    /// <summary>
    ///     Loads definitions from a JSON stream.
    /// </summary>
    public void LoadFromStream(Stream stream, string sourceName)
    {
        using var doc = JsonDocument.Parse(stream);
        LoadFromJsonDocument(doc, sourceName);
    }

    /// <summary>
    ///     Loads definitions from a JSON string.
    /// </summary>
    public void LoadFromJson(string json, string sourceName)
    {
        using var doc = JsonDocument.Parse(json);
        LoadFromJsonDocument(doc, sourceName);
    }

    private void LoadDefinitionsFromResource(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger?.LogWarning("Could not load resource: {Resource}", resourceName);
                return;
            }

            using var doc = JsonDocument.Parse(stream);
            LoadFromJsonDocument(doc, resourceName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading definitions from {Resource}", resourceName);
        }
    }

    private void LoadFromJsonDocument(JsonDocument doc, string sourceName)
    {
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            // Skip meta properties (start with $ or _)
            if (property.Name.StartsWith("$") || property.Name.StartsWith("_"))
                continue;

            _rawDefinitions[property.Name] = property.Value.Clone();
            _logger?.LogDebug("Found definition '{Name}' in {Source}", property.Name, sourceName);
        }
    }

    private void ResolveAllInheritance()
    {
        var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in _rawDefinitions.Keys)
        {
            ResolveDefinition(name, resolving);
        }
    }

    private ResolvedDefinition ResolveDefinition(string name, HashSet<string> resolving)
    {
        // Already resolved?
        if (_resolved.TryGetValue(name, out var existing))
            return existing;

        // Check for circular reference
        if (!resolving.Add(name))
        {
            var chain = string.Join(" -> ", resolving) + " -> " + name;
            throw new InvalidOperationException($"Circular inheritance detected: {chain}");
        }

        if (!_rawDefinitions.TryGetValue(name, out var element))
        {
            throw new KeyNotFoundException($"Definition not found: {name}");
        }

        var inheritanceChain = new List<string> { name };
        var mergedProperties = new Dictionary<string, JsonElement>();

        // Check for Extends
        string? parentName = null;
        if (element.TryGetProperty("Extends", out var extendsElement))
        {
            parentName = extendsElement.GetString();
        }

        // Resolve parent first
        if (!string.IsNullOrEmpty(parentName))
        {
            var parent = ResolveDefinition(parentName, resolving);
            inheritanceChain.AddRange(parent.InheritanceChain);

            // Copy parent properties
            foreach (var (key, value) in parent.Properties)
            {
                mergedProperties[key] = value;
            }
        }

        // Apply this definition's properties (override parent)
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "Extends" || property.Name.StartsWith("$") || property.Name.StartsWith("_"))
                continue;

            mergedProperties[property.Name] = property.Value.Clone();
        }

        var resolved = new ResolvedDefinition
        {
            Name = name,
            InheritanceChain = inheritanceChain,
            Properties = mergedProperties
        };

        _resolved[name] = resolved;
        resolving.Remove(name);

        // Log inheritance chain
        if (inheritanceChain.Count > 1)
        {
            var chainStr = string.Join(" -> ", inheritanceChain);
            _logger?.LogInformation(
                "Resolved definition '{Name}' (chain: {Chain})",
                name, chainStr);
        }
        else
        {
            _logger?.LogDebug("Resolved definition '{Name}' (no inheritance)", name);
        }

        return resolved;
    }

    /// <summary>
    ///     Gets a resolved definition by name.
    /// </summary>
    public ResolvedDefinition? GetDefinition(string name)
    {
        return _resolved.TryGetValue(name, out var def) ? def : null;
    }

    /// <summary>
    ///     Gets all resolved definitions.
    /// </summary>
    public IReadOnlyDictionary<string, ResolvedDefinition> GetAllDefinitions()
    {
        return _resolved;
    }
}

/// <summary>
///     A resolved policy definition with inheritance applied.
/// </summary>
public class ResolvedDefinition
{
    /// <summary>
    ///     Name of this definition.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    ///     Inheritance chain from this definition to root (this -> parent -> grandparent -> ...).
    /// </summary>
    public List<string> InheritanceChain { get; init; } = new();

    /// <summary>
    ///     Merged properties after inheritance resolution.
    /// </summary>
    public Dictionary<string, JsonElement> Properties { get; init; } = new();

    /// <summary>
    ///     Gets a string property value.
    /// </summary>
    public string? GetString(string key)
    {
        return Properties.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }

    /// <summary>
    ///     Gets an integer property value.
    /// </summary>
    public int? GetInt(string key)
    {
        return Properties.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetInt32()
            : null;
    }

    /// <summary>
    ///     Gets a double property value.
    /// </summary>
    public double? GetDouble(string key)
    {
        return Properties.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetDouble()
            : null;
    }

    /// <summary>
    ///     Gets a boolean property value.
    /// </summary>
    public bool? GetBool(string key)
    {
        if (!Properties.TryGetValue(key, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    /// <summary>
    ///     Gets a string array property value.
    /// </summary>
    public List<string>? GetStringArray(string key)
    {
        if (!Properties.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return null;

        return el.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }

    /// <summary>
    ///     Gets a dictionary property value.
    /// </summary>
    public Dictionary<string, string>? GetStringDictionary(string key)
    {
        if (!Properties.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Object)
            return null;

        var dict = new Dictionary<string, string>();
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                dict[prop.Name] = prop.Value.GetString()!;
        }
        return dict;
    }

    /// <summary>
    ///     Formats the inheritance chain as a string for logging.
    /// </summary>
    public string FormatInheritanceChain()
    {
        return InheritanceChain.Count > 1
            ? string.Join(" -> ", InheritanceChain)
            : Name;
    }
}
