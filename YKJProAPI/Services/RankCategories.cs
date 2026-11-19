namespace YKJProAPI.Services;

public static class RankCategories
{
    public const string TCG = "TCG";
    public const string Anime = "Anime";
    public const string Community = "Community";

    public static readonly string[] All = { TCG, Anime, Community };

    public const string AllCsv = "TCG,Anime,Community";

    // Returns the canonical spelling ("tcg" -> "TCG"), or null if unknown.
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return All.FirstOrDefault(c => string.Equals(c, v, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> Parse(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize).Where(c => c != null).Select(c => c!).Distinct().ToList();

    public static string ToCsv(IEnumerable<string> categories) =>
        string.Join(",", All.Where(c => categories.Contains(c)));
}
