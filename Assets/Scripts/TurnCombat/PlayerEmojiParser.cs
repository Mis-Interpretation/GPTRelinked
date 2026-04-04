using System.Collections.Generic;

public static class PlayerEmojiParser
{
    private static readonly Dictionary<string, string> EmojiMeanings = new Dictionary<string, string>
    {
        // Actual unicode characters
        { "\U0001F605", "awkwardly laugh" },
        { "\U0001F608", "demon" },
        { "\U0001F619", "like" },
        { "\U0001F624", "Angry" },
        { "\U0001F628", "Afraid" },
        { "\U0001F62D", "cry hard" },
        { "\U0001F635", "dead" },
        { "\U0001F64C", "surrender" },
        { "\U0001F64F", "collaborate" },
        { "\U0001F60D", "love" },
        
        // Literal string representations from TMP escape sequences
        { "\\U0001F605", "awkwardly laugh" },
        { "\\U0001F608", "demon" },
        { "\\U0001F619", "like" },
        { "\\U0001F624", "Angry" },
        { "\\U0001F628", "Afraid" },
        { "\\U0001F62D", "cry hard" },
        { "\\U0001F635", "dead" },
        { "\\U0001F64C", "surrender" },
        { "\\U0001F64F", "collaborate" },
        { "\\U0001F60D", "love" },
        
        // Also support lowercase escape variations just in case
        { "\\u0001f605", "awkwardly laugh" },
        { "\\u0001f608", "demon" },
        { "\\u0001f619", "like" },
        { "\\u0001f624", "Angry" },
        { "\\u0001f628", "Afraid" },
        { "\\u0001f62d", "cry hard" },
        { "\\u0001f635", "dead" },
        { "\\u0001f64c", "surrender" },
        { "\\u0001f64f", "collaborate" },
        { "\\u0001f60d", "love" }
    };

    /// <summary>
    /// Parses emojis in the input string and replaces them with their text representation (e.g. "[Emoji: meaning]").
    /// </summary>
    public static string ParseEmojisToText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        string result = input;
        foreach (var kvp in EmojiMeanings)
        {
            if (result.Contains(kvp.Key))
            {
                result = result.Replace(kvp.Key, $" [Emoji: {kvp.Value}] ");
            }
        }

        // Clean up any potential double spaces created by the replacement
        result = result.Replace("  ", " ").Trim();
        
        return result;
    }
}
