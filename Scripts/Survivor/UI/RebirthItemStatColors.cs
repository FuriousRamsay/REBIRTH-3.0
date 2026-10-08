using System.Text.RegularExpressions;

// Apply the same sign-based negative color to every custom item-stat presentation.
public static class RebirthItemStatColors
{
    private static readonly Regex Tokens = new Regex(@"\[[^\]]*\]|[-−]\d+(?:[.,]\d+)*(?:%)?", RegexOptions.Compiled);
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Tokens.Replace(text, match => match.Value[0] == '['
            ? match.Value : "[FF0000]" + match.Value + "[-]");
    }
}
