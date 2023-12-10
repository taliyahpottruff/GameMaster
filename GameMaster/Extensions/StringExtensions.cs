using System.Text.RegularExpressions;

namespace GameMaster.Extensions;

public static class StringExtensions
{
    public static string RebuildParts(this string[] parts, int startingIndex = 0)
    {
        string result = "";
        for (int i = startingIndex; i < parts.Length; i++)
        {
            result += parts[i];

            if (i < parts.Length - 1)
                result += " ";
        }

        return result;
    }

    public static string Sanitize(this string s)
    {
        Regex rgx = new Regex("[^a-zA-Z0-9 -]");
        return rgx.Replace(s, "");
    }
}