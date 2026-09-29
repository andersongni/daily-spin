namespace RoletaDaDaily;

internal sealed record ParticipantParseResult(IReadOnlyList<string> Names, int DuplicateCount);

internal static class ParticipantService
{
    public static ParticipantParseResult Parse(string? input)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicates = 0;

        foreach (string line in (input ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string name = line.Trim();
            if (name.Length == 0)
                continue;

            if (seen.Add(name))
                names.Add(name);
            else
                duplicates++;
        }

        return new ParticipantParseResult(names, duplicates);
    }

    public static string Format(IEnumerable<string> names) => string.Join(Environment.NewLine, names);
}
