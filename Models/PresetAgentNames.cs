namespace DynaDocs.Models;

/// <summary>
/// Preset agent name sets
/// </summary>
public static class PresetAgentNames
{
    /// <summary>
    /// Primary set of 26 agent names (A-Z)
    /// </summary>
    public static readonly IReadOnlyList<string> Set1 = new[]
    {
        "Adele", "Brian", "Charlie", "Dexter", "Emma", "Frank",
        "Grace", "Henry", "Iris", "Jack", "Kate", "Leo",
        "Mia", "Noah", "Olivia", "Paul", "Quinn", "Rose",
        "Sam", "Tara", "Uma", "Victor", "Wendy", "Xavier",
        "Yara", "Zack"
    };

    /// <summary>
    /// Overflow set of 26 more agent names
    /// </summary>
    public static readonly IReadOnlyList<string> Set2 = new[]
    {
        "Alfred", "Bella", "Carla", "Dylan", "Ethan", "Fiona",
        "George", "Holly", "Ivan", "Julia", "Kevin", "Luna",
        "Marcus", "Nadia", "Oscar", "Penny", "Quentin", "Rita",
        "Steve", "Tina", "Ulrich", "Vera", "Walter", "Xena",
        "Yuri", "Zara"
    };

    /// <summary>
    /// Get agent names up to the specified count
    /// </summary>
    public static List<string> GetNames(int count)
    {
        var names = new List<string>();

        // Add from Set1 first
        for (int i = 0; i < Math.Min(count, Set1.Count); i++)
            names.Add(Set1[i]);

        // Add from Set2 if needed
        if (count > Set1.Count)
        {
            var remaining = count - Set1.Count;
            for (int i = 0; i < Math.Min(remaining, Set2.Count); i++)
                names.Add(Set2[i]);
        }

        return names;
    }

    /// <summary>
    /// Get agent name from letter (A=Adele, B=Brian, etc.)
    /// </summary>
    public static string? GetNameFromLetter(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        var index = letter - 'A';

        if (index >= 0 && index < Set1.Count)
            return Set1[index];

        return null;
    }

    /// <summary>
    /// Get letter from agent name
    /// </summary>
    public static char? GetLetterFromName(string name)
    {
        var index = Set1.ToList().FindIndex(n =>
            n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            return (char)('A' + index);

        return null;
    }
}
