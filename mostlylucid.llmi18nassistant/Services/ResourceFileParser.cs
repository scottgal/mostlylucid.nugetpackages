using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Parses .resx and JSON resource files
/// </summary>
public partial class ResourceFileParser : IResourceFileParser
{
    private readonly LlmI18nAssistantConfig _config;
    private readonly ILogger<ResourceFileParser> _logger;

    public ResourceFileParser(
        ILogger<ResourceFileParser> logger,
        IOptions<LlmI18nAssistantConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc />
    public async Task<ResourceFile> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Resource file not found: {filePath}");

        var fileType = GetFileType(filePath);
        await using var stream = File.OpenRead(filePath);

        var resourceFile = await ParseAsync(stream, fileType, Path.GetFileName(filePath), cancellationToken);
        resourceFile.FilePath = filePath;

        return resourceFile;
    }

    /// <inheritdoc />
    public async Task<ResourceFile> ParseAsync(Stream stream, ResourceFileType fileType, string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        return fileType switch
        {
            ResourceFileType.Resx => await ParseResxAsync(stream, fileName, cancellationToken),
            ResourceFileType.Json => await ParseJsonAsync(stream, fileName, cancellationToken),
            ResourceFileType.Properties => await ParsePropertiesAsync(stream, fileName, cancellationToken),
            _ => throw new NotSupportedException($"File type {fileType} is not supported")
        };
    }

    /// <inheritdoc />
    public async Task WriteAsync(TranslationResult result, string outputPath,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var fileType = GetFileType(outputPath);
        await using var stream = File.Create(outputPath);
        await WriteAsync(result, stream, fileType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteAsync(TranslationResult result, Stream stream, ResourceFileType fileType,
        CancellationToken cancellationToken = default)
    {
        switch (fileType)
        {
            case ResourceFileType.Resx:
                await WriteResxAsync(result, stream, cancellationToken);
                break;
            case ResourceFileType.Json:
                await WriteJsonAsync(result, stream, cancellationToken);
                break;
            case ResourceFileType.Properties:
                await WritePropertiesAsync(result, stream, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"File type {fileType} is not supported");
        }
    }

    /// <inheritdoc />
    public ResourceFileType GetFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".resx" => ResourceFileType.Resx,
            ".json" => ResourceFileType.Json,
            ".properties" => ResourceFileType.Properties,
            _ => throw new NotSupportedException($"File extension {extension} is not supported")
        };
    }

    /// <inheritdoc />
    public bool Supports(ResourceFileType fileType)
    {
        return fileType is ResourceFileType.Resx or ResourceFileType.Json or ResourceFileType.Properties;
    }

    private async Task<ResourceFile> ParseResxAsync(Stream stream, string? fileName,
        CancellationToken cancellationToken)
    {
        var doc = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, cancellationToken);

        var resourceFile = new ResourceFile
        {
            FileName = fileName != null ? Path.GetFileNameWithoutExtension(fileName) : null,
            FileType = ResourceFileType.Resx,
            SourceLanguage = _config.DefaultSourceLanguage
        };

        // Extract metadata from header
        foreach (var resheader in doc.Descendants("resheader"))
        {
            var name = resheader.Attribute("name")?.Value;
            var value = resheader.Element("value")?.Value;
            if (name != null && value != null)
                resourceFile.Metadata[name] = value;
        }

        // Extract data entries
        foreach (var data in doc.Descendants("data"))
        {
            var name = data.Attribute("name")?.Value;
            var value = data.Element("value")?.Value;
            var comment = data.Element("comment")?.Value;
            var type = data.Attribute("type")?.Value;

            // Skip entries with type attribute (these are typically binary resources)
            if (name == null || type != null)
                continue;

            var entry = new ResourceEntry
            {
                Key = name,
                Value = value ?? string.Empty,
                Comment = comment
            };

            // Determine if this entry should be translated
            entry.ShouldTranslate = ShouldTranslateEntry(entry);

            resourceFile.Entries.Add(entry);
        }

        _logger.LogInformation("Parsed {Count} entries from resx file {FileName}",
            resourceFile.Entries.Count, fileName);

        return resourceFile;
    }

    private async Task<ResourceFile> ParseJsonAsync(Stream stream, string? fileName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);

        var resourceFile = new ResourceFile
        {
            FileName = fileName != null ? Path.GetFileNameWithoutExtension(fileName) : null,
            FileType = ResourceFileType.Json,
            SourceLanguage = _config.DefaultSourceLanguage
        };

        var document = JsonDocument.Parse(json);
        ParseJsonElement(document.RootElement, "", resourceFile.Entries);

        _logger.LogInformation("Parsed {Count} entries from JSON file {FileName}",
            resourceFile.Entries.Count, fileName);

        return resourceFile;
    }

    private void ParseJsonElement(JsonElement element, string prefix, List<ResourceEntry> entries)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    ParseJsonElement(property.Value, key, entries);
                }

                break;

            case JsonValueKind.String:
                var entry = new ResourceEntry
                {
                    Key = prefix,
                    Value = element.GetString() ?? string.Empty
                };
                entry.ShouldTranslate = ShouldTranslateEntry(entry);
                entries.Add(entry);
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ParseJsonElement(item, $"{prefix}[{index}]", entries);
                    index++;
                }

                break;
        }
    }

    private async Task<ResourceFile> ParsePropertiesAsync(Stream stream, string? fileName,
        CancellationToken cancellationToken)
    {
        var resourceFile = new ResourceFile
        {
            FileName = fileName != null ? Path.GetFileNameWithoutExtension(fileName) : null,
            FileType = ResourceFileType.Properties,
            SourceLanguage = _config.DefaultSourceLanguage
        };

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#') ||
                line.TrimStart().StartsWith('!'))
                continue;

            var separatorIndex = line.IndexOfAny(['=', ':']);
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // Handle escaped values
            value = UnescapePropertiesValue(value);

            var entry = new ResourceEntry
            {
                Key = key,
                Value = value
            };
            entry.ShouldTranslate = ShouldTranslateEntry(entry);

            resourceFile.Entries.Add(entry);
        }

        _logger.LogInformation("Parsed {Count} entries from properties file {FileName}",
            resourceFile.Entries.Count, fileName);

        return resourceFile;
    }

    private bool ShouldTranslateEntry(ResourceEntry entry)
    {
        // Check skip patterns for keys
        foreach (var pattern in _config.ValueTransformation.SkipKeyPatterns)
            if (Regex.IsMatch(entry.Key, pattern))
            {
                entry.SkipReason = $"Key matches skip pattern: {pattern}";
                return false;
            }

        // Check skip patterns for values
        foreach (var pattern in _config.ValueTransformation.SkipValuePatterns)
            if (Regex.IsMatch(entry.Value, pattern))
            {
                entry.SkipReason = $"Value matches skip pattern: {pattern}";
                return false;
            }

        // Skip empty values
        if (string.IsNullOrWhiteSpace(entry.Value))
        {
            entry.SkipReason = "Empty value";
            return false;
        }

        // Skip values that are only numbers
        if (NumberOnlyRegex().IsMatch(entry.Value))
        {
            entry.SkipReason = "Numeric value only";
            return false;
        }

        return true;
    }

    private async Task WriteResxAsync(TranslationResult result, Stream stream,
        CancellationToken cancellationToken)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("root")
        );

        var root = doc.Root!;

        // Add standard resx headers
        AddResxHeaders(root);

        // Add schema
        AddResxSchema(root);

        // Add translated entries
        foreach (var entry in result.Entries)
        {
            var value = entry.TranslatedValue ?? entry.Value;

            var dataElement = new XElement("data",
                new XAttribute("name", entry.Key),
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", value)
            );

            if (!string.IsNullOrEmpty(entry.Comment))
                dataElement.Add(new XElement("comment", entry.Comment));

            root.Add(dataElement);
        }

        await doc.SaveAsync(stream, SaveOptions.None, cancellationToken);
    }

    private static void AddResxHeaders(XElement root)
    {
        root.Add(new XElement("resheader",
            new XAttribute("name", "resmimetype"),
            new XElement("value", "text/microsoft-resx")));

        root.Add(new XElement("resheader",
            new XAttribute("name", "version"),
            new XElement("value", "2.0")));

        root.Add(new XElement("resheader",
            new XAttribute("name", "reader"),
            new XElement("value",
                "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")));

        root.Add(new XElement("resheader",
            new XAttribute("name", "writer"),
            new XElement("value",
                "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")));
    }

    private static void AddResxSchema(XElement root)
    {
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        XNamespace msdata = "urn:schemas-microsoft-com:xml-msdata";

        root.Add(new XElement(xs + "schema",
            new XAttribute("id", "root"),
            new XAttribute(XNamespace.Xmlns + "xs", xs),
            new XAttribute(XNamespace.Xmlns + "msdata", msdata)
        ));
    }

    private async Task WriteJsonAsync(TranslationResult result, Stream stream,
        CancellationToken cancellationToken)
    {
        var translations = new Dictionary<string, object>();

        foreach (var entry in result.Entries)
        {
            var value = entry.TranslatedValue ?? entry.Value;
            SetNestedValue(translations, entry.Key, value);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        await JsonSerializer.SerializeAsync(stream, translations, options, cancellationToken);
    }

    private static void SetNestedValue(Dictionary<string, object> dict, string key, string value)
    {
        var parts = key.Split('.');
        var current = dict;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];

            // Handle array notation
            var arrayMatch = Regex.Match(part, @"^(.+)\[(\d+)\]$");
            if (arrayMatch.Success) part = arrayMatch.Groups[1].Value;
            // For simplicity, we'll flatten arrays in the output
            if (!current.TryGetValue(part, out var child))
            {
                child = new Dictionary<string, object>();
                current[part] = child;
            }

            if (child is Dictionary<string, object> childDict)
                current = childDict;
            else
                return; // Type mismatch, skip
        }

        current[parts[^1]] = value;
    }

    private async Task WritePropertiesAsync(TranslationResult result, Stream stream,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);

        await writer.WriteLineAsync($"# Generated by LlmI18nAssistant on {DateTime.UtcNow:O}");
        await writer.WriteLineAsync($"# Target language: {result.TargetLanguage}");
        await writer.WriteLineAsync();

        foreach (var entry in result.Entries)
        {
            var value = entry.TranslatedValue ?? entry.Value;
            var escapedValue = EscapePropertiesValue(value);

            await writer.WriteLineAsync($"{entry.Key}={escapedValue}");
        }
    }

    private static string UnescapePropertiesValue(string value)
    {
        return value
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r")
            .Replace("\\\\", "\\");
    }

    private static string EscapePropertiesValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r");
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumberOnlyRegex();
}