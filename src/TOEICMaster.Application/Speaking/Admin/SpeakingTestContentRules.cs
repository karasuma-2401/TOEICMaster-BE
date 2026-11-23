using System.Text;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Admin;

/// <summary>Content identity is scoped to a test, including questions in different Parts.</summary>
public static class SpeakingTestContentRules
{
    public static string Normalize(string? text)
    {
        var result = new StringBuilder();
        var separator = false;
        foreach (var character in (text ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLowerInvariant().EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(character))
            {
                if (separator && result.Length > 0)
                    result.Append(' ');
                result.Append(character.ToString());
                separator = false;
            }
            else
                separator = true;
        }
        return result.ToString();
    }

    public static bool HasDistinctQuestions(IReadOnlyList<string?>? questions) =>
        questions is null || questions.Where(question => !string.IsNullOrWhiteSpace(question))
            .Select(Normalize).GroupBy(text => text).All(group => group.Count() == 1);

    public static IReadOnlyList<string> Check(
        IEnumerable<(int Part, int Sequence, SpeakingContentDto Content)> slots,
        IReadOnlyDictionary<Guid, SpeakingMediaAsset> images)
    {
        var texts = new List<(int Position, string Text)>();
        var pictures = new List<(int Position, Guid Id, string? Hash)>();
        foreach (var (part, sequence, content) in slots)
        {
            var position = part switch { 1 => sequence, 2 => sequence + 2, 3 => 5, 4 => 8, 5 => 11, _ => 0 };
            if (part == 2 && content.ImageAssetId is { } id)
                pictures.Add((position, id, images.GetValueOrDefault(id)?.Sha256));
            else if (part is 3 or 4)
                texts.AddRange((content.Questions ?? []).Select((question, index) =>
                    (position + index, Normalize(question))));
            else
                texts.Add((position, Normalize(part == 1 ? content.Passage : content.Prompt)));
        }

        var errors = new List<string>();
        for (var left = 0; left < texts.Count; left++)
            for (var right = left + 1; right < texts.Count; right++)
                if (texts[left].Text.Length > 0 && texts[left].Text == texts[right].Text)
                    errors.Add($"Question {texts[left].Position} and Question {texts[right].Position} have duplicate text content.");
        for (var left = 0; left < pictures.Count; left++)
            for (var right = left + 1; right < pictures.Count; right++)
                if (pictures[left].Id == pictures[right].Id ||
                    !string.IsNullOrEmpty(pictures[left].Hash) && string.Equals(pictures[left].Hash,
                        pictures[right].Hash, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Question {pictures[left].Position} and Question {pictures[right].Position} use the same image.");
        return errors;
    }

    internal static async Task<IReadOnlyList<string>> CheckAsync(
        IReadOnlyList<(int Part, int Sequence, SpeakingContentDto Content)> slots,
        ISpeakingAdminRepository repository, CancellationToken cancellationToken)
    {
        var images = new Dictionary<Guid, SpeakingMediaAsset>();
        foreach (var id in slots.Where(slot => slot.Part == 2).Select(slot => slot.Content.ImageAssetId)
                     .OfType<Guid>().Distinct())
        {
            var image = await repository.FindMediaAssetAsync(id, cancellationToken);
            if (image is not null)
                images[id] = image;
        }
        return Check(slots, images);
    }
}
