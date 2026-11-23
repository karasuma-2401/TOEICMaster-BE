using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TOEICMaster.Application.Knowledge;

public static partial class KnowledgeSlug
{
    public static string Generate(string source, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var replaced = source.Trim()
            .Replace('\u0111', 'd')
            .Replace('\u0110', 'd');
        var decomposed = replaced.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }

        var ascii = builder.ToString().Normalize(NormalizationForm.FormC);
        var hyphenated = NonAlphaNumericRegex().Replace(ascii, "-").Trim('-');
        if (hyphenated.Length > maxLength)
            hyphenated = hyphenated[..maxLength].TrimEnd('-');
        return hyphenated;
    }

    public static bool IsValid(string? slug, int maxLength) =>
        !string.IsNullOrWhiteSpace(slug) &&
        slug.Length <= maxLength &&
        ValidSlugRegex().IsMatch(slug);

    public static string Resolve(string? explicitSlug, string fallbackSource, int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(explicitSlug))
            return explicitSlug.Trim().ToLowerInvariant();
        return Generate(fallbackSource, maxLength);
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidSlugRegex();
}
