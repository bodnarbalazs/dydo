namespace DynaDocs.Models;

/// <summary>
/// Preset agent name sets
/// </summary>
public static class PresetAgentNames
{
    /// <summary>
    /// Maximum number of agents supported (26 names × 4 sets)
    /// </summary>
    public const int MaxAgentCount = 104;

    /// <summary>
    /// Primary set of 26 agent names (A-Z)
    /// </summary>
    public static readonly IReadOnlyList<string> Set1 = new[]
    {
        "Adele", "Brian", "Charlie", "Dexter", "Emma", "Frank",
        "Grace", "Henry", "Iris", "Jack", "Kate", "Leo",
        "Mia", "Noah", "Olivia", "Paul", "Quinn", "Rose",
        "Sam", "Tara", "Uma", "Victor", "Wendy", "Xavier",
        "Yara", "Zelda"
    };

    /// <summary>
    /// Second set of 26 agent names
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
    /// Third set of 26 agent names
    /// </summary>
    public static readonly IReadOnlyList<string> Set3 = new[]
    {
        "Amber", "Blake", "Cora", "Dante", "Elena", "Felix",
        "Greta", "Hugo", "Ingrid", "Jasper", "Kira", "Liam",
        "Maya", "Nico", "Opal", "Pierce", "Quincy", "Raven",
        "Sienna", "Tobias", "Ursula", "Vince", "Willow", "Xander",
        "Yasmin", "Zeke"
    };

    /// <summary>
    /// Fourth set of 26 agent names
    /// </summary>
    public static readonly IReadOnlyList<string> Set4 = new[]
    {
        "Aurora", "Bruno", "Celeste", "Dominic", "Esther", "Floyd",
        "Gloria", "Hector", "Ivy", "Jerome", "Kendra", "Lance",
        "Miriam", "Nelson", "Ophelia", "Preston", "Quorra", "Rocco",
        "Selena", "Trevor", "Unity", "Vaughn", "Whitney", "Ximena",
        "York", "Zack"
    };

    /// <summary>
    /// Get agent names up to the specified count
    /// </summary>
    public static List<string> GetNames(int count)
    {
        var allSets = new[] { Set1, Set2, Set3, Set4 };
        var names = new List<string>();

        foreach (var set in allSets)
        {
            if (names.Count >= count) break;

            var remaining = count - names.Count;
            for (int i = 0; i < Math.Min(remaining, set.Count); i++)
                names.Add(set[i]);
        }

        return names;
    }
}
