using System.IO;

namespace OcrSnip.Ocr;

public static class ModelConfig
{
    public static string[] LoadCharacters(string configPath)
    {
        var characters = new List<string>();
        var inDictionary = false;
        foreach (var rawLine in File.ReadLines(configPath))
        {
            var line = rawLine;
            if (line.Trim() == "character_dict:")
            {
                inDictionary = true;
                continue;
            }

            if (!inDictionary)
            {
                continue;
            }

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                break;
            }

            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmed[2..];
            characters.Add(UnquoteYamlScalar(value));
        }

        if (!characters.Contains(" "))
        {
            characters.Add(" ");
        }

        return characters.ToArray();
    }

    private static string UnquoteYamlScalar(string value)
    {
        if (value == "''")
        {
            return "'";
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }
}
