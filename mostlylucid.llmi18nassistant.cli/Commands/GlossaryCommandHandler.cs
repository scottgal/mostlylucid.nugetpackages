using System.Text.Json;
using System.Xml.Linq;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Cli.Commands;

public class GlossaryCommandHandler
{
    public async Task<int> InitAsync(FileInfo path)
    {
        if (path.Exists)
        {
            Console.Error.WriteLine($"Error: File already exists: {path.FullName}");
            return 1;
        }

        var glossary = new Glossary
        {
            Name = Path.GetFileNameWithoutExtension(path.Name),
            Description = "Translation glossary",
            SourceLanguage = "en",
            TargetLanguages = ["de", "fr", "es"],
            Entries =
            [
                new GlossaryEntry
                {
                    SourceTerm = "example",
                    SourceLanguage = "en",
                    Translations = new Dictionary<string, string>
                    {
                        ["de"] = "Beispiel",
                        ["fr"] = "exemple",
                        ["es"] = "ejemplo"
                    },
                    Context = "Sample glossary entry"
                }
            ]
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var directory = path.DirectoryName;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(path.FullName);
        await JsonSerializer.SerializeAsync(stream, glossary, options);

        Console.WriteLine($"Created glossary file: {path.FullName}");
        Console.WriteLine();
        Console.WriteLine("Edit the file to add your terminology, then use it with:");
        Console.WriteLine($"  llm-i18n translate <file> -t de -g {path.FullName}");

        return 0;
    }

    public async Task<int> ImportAsync(FileInfo glossaryFile, FileInfo sourceFile, string language)
    {
        if (!sourceFile.Exists)
        {
            Console.Error.WriteLine($"Error: Source file not found: {sourceFile.FullName}");
            return 1;
        }

        Glossary glossary;

        if (glossaryFile.Exists)
        {
            // Load existing glossary
            var json = await File.ReadAllTextAsync(glossaryFile.FullName);
            glossary = JsonSerializer.Deserialize<Glossary>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new Glossary();
        }
        else
        {
            // Create new glossary
            glossary = new Glossary
            {
                Name = Path.GetFileNameWithoutExtension(glossaryFile.Name),
                SourceLanguage = "en"
            };
        }

        // Ensure target language is in the list
        if (!glossary.TargetLanguages.Contains(language))
            glossary.TargetLanguages.Add(language);

        // Parse source file to extract terms
        var extension = sourceFile.Extension.ToLowerInvariant();
        var entries = await ExtractEntriesAsync(sourceFile.FullName, extension);

        var addedCount = 0;
        var updatedCount = 0;

        foreach (var (key, value) in entries)
        {
            // Skip empty or very short values
            if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
                continue;

            // Check if entry exists
            var existing =
                glossary.Entries.FirstOrDefault(e => e.SourceTerm.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Update translation
                existing.Translations[language] = value;
                updatedCount++;
            }
            else
            {
                // Add new entry
                glossary.Entries.Add(new GlossaryEntry
                {
                    SourceTerm = key,
                    SourceLanguage = glossary.SourceLanguage,
                    Translations = new Dictionary<string, string> { [language] = value }
                });
                addedCount++;
            }
        }

        glossary.LastUpdated = DateTime.UtcNow;

        // Save glossary
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var directory = glossaryFile.DirectoryName;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(glossaryFile.FullName);
        await JsonSerializer.SerializeAsync(stream, glossary, options);

        Console.WriteLine($"Glossary updated: {glossaryFile.FullName}");
        Console.WriteLine($"  Added: {addedCount} entries");
        Console.WriteLine($"  Updated: {updatedCount} entries");
        Console.WriteLine($"  Total: {glossary.Entries.Count} entries");

        return 0;
    }

    private static async Task<Dictionary<string, string>> ExtractEntriesAsync(string filePath, string extension)
    {
        var entries = new Dictionary<string, string>();

        if (extension == ".json")
        {
            var json = await File.ReadAllTextAsync(filePath);
            var doc = JsonDocument.Parse(json);
            ExtractJsonEntries(doc.RootElement, "", entries);
        }
        else if (extension == ".resx")
        {
            var xml = await File.ReadAllTextAsync(filePath);
            var doc = XDocument.Parse(xml);
            foreach (var data in doc.Descendants("data"))
            {
                var name = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;
                if (name != null && value != null)
                    entries[name] = value;
            }
        }

        return entries;
    }

    private static void ExtractJsonEntries(JsonElement element, string prefix, Dictionary<string, string> entries)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    ExtractJsonEntries(prop.Value, key, entries);
                }

                break;
            case JsonValueKind.String:
                entries[prefix] = element.GetString() ?? "";
                break;
        }
    }
}