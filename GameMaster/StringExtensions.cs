namespace GameMaster;

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
}