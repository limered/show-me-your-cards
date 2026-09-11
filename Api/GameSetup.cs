using System.Security.Cryptography;

public static class GameSetup
{
    public const int TableSize = 12;
    public const string Fib = "1,2,3,5,8,13,21";
    public const string Incremental = "1,2,3,5,8";
    public const string DefaultTimer = "2m";
    public static readonly string[] Timers = ["30s", "1m", "2m", "5m", "off"];

    private static readonly string[] Adjectives =
    [
        "amber", "sleepy", "cosmic", "velvet", "fizzy", "honey", "misty", "neon", "pebble", "drowsy",
        "maple", "jazzy", "cloudy", "ember", "fable", "ginger", "hazy", "ivory", "jolly", "kelp",
        "lunar", "mossy", "nimble", "oat", "pastel", "quartz", "rusty", "snug", "toasty", "umber",
        "vanilla", "wobbly", "yarn", "zesty", "birch", "cocoa", "dandy", "elm", "fern", "groovy",
        "harbor", "inky", "jumbo", "knit", "lilac", "meadow", "noodle", "orbit", "plum", "cedar",
    ];

    private static readonly string[] Subjects =
    [
        "panda", "otter", "mochi", "comet", "badger", "waffle", "pixel", "sprout", "lantern", "marble",
        "turnip", "banjo", "cactus", "donut", "fiddle", "grove", "hedgehog", "igloo", "juniper", "koala",
        "llama", "muffin", "narwhal", "onion", "pretzel", "quokka", "rover", "scone", "taco", "urchin",
        "violet", "walrus", "xylophone", "yeti", "zephyr", "acorn", "button", "clover", "dumpling", "eclair",
        "gopher", "hammock", "ibis", "jigsaw", "kite", "lotus", "magnet", "novel", "oven", "prism",
    ];

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string NewSessionId()
    {
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    public static string PickName(IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken);
        for (var i = 0; i < 100; i++)
        {
            var name = $"{Title(Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)])}-" +
                       $"{Title(Subjects[RandomNumberGenerator.GetInt32(Subjects.Length)])}";
            if (used.Add(name))
                return name;
        }
        return $"player-{Guid.NewGuid():N}"[..15];
    }

    public static string ResolveDeck(string? deck) => string.IsNullOrWhiteSpace(deck)
        ? Fib
        : deck.Trim().ToLowerInvariant() switch { "fib" or "fibonacci" => Fib, "inc" or "incremental" => Incremental, _ => deck.Trim() };

    public static string ResolveTimer(string? timer) =>
        Timers.Contains(timer) ? timer! : DefaultTimer;

    private static string Title(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    public static string[] Cards(string deck) =>
        deck.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool IsNumeric(string card) => double.TryParse(card, out _);

    public static (string Card, double Mean)? Result(string deck, IEnumerable<string> played)
    {
        var numbers = played.Where(IsNumeric).Select(double.Parse).ToList();
        if (numbers.Count == 0)
            return null;
        var mean = numbers.Average();
        var deckNumbers = Cards(deck).Where(IsNumeric).ToList();
        var rounded = deckNumbers.FirstOrDefault(c => double.Parse(c) >= mean)
                      ?? deckNumbers.LastOrDefault();
        return rounded is null ? null : (rounded, mean);
    }
}
