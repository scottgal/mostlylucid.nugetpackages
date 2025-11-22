using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects personal names using pattern matching and common name databases.
/// This is a heuristic-based detector as name detection is inherently fuzzy.
/// </summary>
public class NameDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.Name;
    public override string Name => "NameDetector";
    public override int Priority => 50; // Lower priority due to potential false positives
    protected override double DefaultConfidence => 0.7;

    // Pattern for potential names: Capitalized words, possibly with prefixes
    protected override string Pattern =>
        @"(?<![a-zA-Z])(?:(?:Mr|Mrs|Ms|Miss|Dr|Prof|Sir|Lord|Lady)\.?\s+)?[A-Z][a-z]{1,20}(?:\s+[A-Z][a-z]{1,20}){1,3}(?![a-zA-Z])";

    // Common first names (top names from various countries)
    private static readonly HashSet<string> CommonFirstNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Male names
        "James", "John", "Robert", "Michael", "William", "David", "Richard", "Joseph", "Thomas", "Charles",
        "Christopher", "Daniel", "Matthew", "Anthony", "Mark", "Donald", "Steven", "Paul", "Andrew", "Joshua",
        "Kenneth", "Kevin", "Brian", "George", "Timothy", "Ronald", "Edward", "Jason", "Jeffrey", "Ryan",
        "Jacob", "Gary", "Nicholas", "Eric", "Jonathan", "Stephen", "Larry", "Justin", "Scott", "Brandon",
        "Benjamin", "Samuel", "Raymond", "Gregory", "Frank", "Alexander", "Patrick", "Jack", "Dennis", "Jerry",

        // Female names
        "Mary", "Patricia", "Jennifer", "Linda", "Elizabeth", "Barbara", "Susan", "Jessica", "Sarah", "Karen",
        "Lisa", "Nancy", "Betty", "Margaret", "Sandra", "Ashley", "Kimberly", "Emily", "Donna", "Michelle",
        "Dorothy", "Carol", "Amanda", "Melissa", "Deborah", "Stephanie", "Rebecca", "Sharon", "Laura", "Cynthia",
        "Kathleen", "Amy", "Angela", "Shirley", "Anna", "Brenda", "Pamela", "Emma", "Nicole", "Helen",
        "Samantha", "Katherine", "Christine", "Debra", "Rachel", "Carolyn", "Janet", "Catherine", "Maria", "Heather",

        // International names
        "Mohammed", "Ahmed", "Ali", "Muhammad", "Ibrahim", "Omar", "Hassan", "Youssef", "Khalid", "Fatima",
        "Wei", "Fang", "Ming", "Chen", "Wang", "Li", "Zhang", "Liu", "Yang", "Huang",
        "Raj", "Priya", "Amit", "Neha", "Arun", "Deepak", "Sanjay", "Anita", "Ravi", "Sunita",
        "Hans", "Klaus", "Wolfgang", "Jürgen", "Dieter", "Petra", "Monika", "Sabine", "Andrea", "Claudia",
        "Jean", "Pierre", "Michel", "Philippe", "Marie", "Nathalie", "Isabelle", "Sophie", "Nicolas", "Francois"
    };

    // Common last names
    private static readonly HashSet<string> CommonLastNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts",
        "Turner", "Phillips", "Evans", "Parker", "Edwards", "Collins", "Stewart", "Morris", "Murphy", "Cook",
        "Rogers", "Morgan", "Peterson", "Cooper", "Reed", "Bailey", "Bell", "Gomez", "Kelly", "Howard",
        "Ward", "Cox", "Diaz", "Richardson", "Wood", "Watson", "Brooks", "Bennett", "Gray", "James"
    };

    // Words that look like names but aren't (common false positives)
    private static readonly HashSet<string> ExcludedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
        "North", "South", "East", "West", "Central", "New", "Old", "Great", "Little", "Upper", "Lower",
        "The", "This", "That", "These", "Those", "Here", "There", "Where", "When", "What", "Which",
        "Please", "Thank", "Hello", "Goodbye", "Welcome", "Sorry", "Dear", "Regards",
        "Error", "Warning", "Info", "Debug", "Trace", "Success", "Failure", "Exception",
        "True", "False", "Null", "None", "Empty", "Default", "Custom", "Generic"
    };

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var potentialName = match.Value.Trim();
        var words = potentialName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Must have at least 2 parts (first and last name)
        if (words.Length < 2)
            return false;

        // Filter out excluded words
        if (words.Any(w => ExcludedWords.Contains(w.TrimEnd('.'))))
            return false;

        // At least one part should be a known name
        var hasKnownFirstName = words.Any(w => CommonFirstNames.Contains(w.TrimEnd('.')));
        var hasKnownLastName = words.Any(w => CommonLastNames.Contains(w));

        return hasKnownFirstName || hasKnownLastName;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var potentialName = match.Value.Trim();
        var words = potentialName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var confidence = 0.6;

        // Check if first word matches common first names
        if (words.Length > 0 && CommonFirstNames.Contains(words[0].TrimEnd('.')))
            confidence += 0.15;

        // Check if last word matches common last names
        if (words.Length > 1 && CommonLastNames.Contains(words[^1]))
            confidence += 0.15;

        // Higher confidence with title prefix
        var prefixes = new[] { "Mr", "Mrs", "Ms", "Miss", "Dr", "Prof", "Sir" };
        if (prefixes.Any(p => potentialName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            confidence += 0.1;

        // Check context for name-related keywords
        var contextStart = Math.Max(0, match.Index - 20);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 20);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var nameKeywords = new[] { "name:", "user:", "customer:", "client:", "patient:", "contact:", "from:", "to:" };
        if (nameKeywords.Any(context.Contains))
            confidence += 0.15;

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
