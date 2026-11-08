using System.Text;

namespace TOEICMaster.Application.Speaking.Admin;

/// <summary>Content of one bank exercise, used to detect duplicates across the whole bank.</summary>
public sealed record SpeakingExerciseContentSource(Guid ExerciseId, string Code, string ContentJson,
    string? ImageSha256);

/// <summary>One comparable piece of text; <paramref name="Field"/> is the form field that holds it.</summary>
internal sealed record SpeakingContentUnit(string Field, string Suffix, string Text);

/// <summary>An exercise placed in a test; <paramref name="ImageKey"/> identifies its picture by content.</summary>
internal sealed record SpeakingSlotContent(string Label, SpeakingContentDto Content, string? ImageKey);

internal sealed record SpeakingContentFailure(string Field, string Message);

/// <summary>Pure duplicate rules for Speaking content (ADR-002); performs no I/O.</summary>
internal static class SpeakingTestContentRules
{
    /// <summary>NFKC, lower case, punctuation to spaces, whitespace collapsed; so wording differences do not hide a copy.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var text = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool HasDistinctQuestions(IReadOnlyList<string?>? questions)
    {
        var texts = (questions ?? []).Select(Normalize).Where(text => text.Length > 0).ToArray();
        return texts.Distinct(StringComparer.Ordinal).Count() == texts.Length;
    }

    public static IReadOnlyList<SpeakingContentUnit> Units(SpeakingContentDto content)
    {
        IEnumerable<SpeakingContentUnit> units = content.Part switch
        {
            1 => [new("Passage", string.Empty, Normalize(content.Passage))],
            3 or 4 => (content.Questions ?? []).Select((question, index) =>
                new SpeakingContentUnit($"Questions[{index}]", $" · câu {index + 1}", Normalize(question))),
            5 => [new("Prompt", string.Empty, Normalize(content.Prompt))],
            _ => []
        };
        return units.Where(unit => unit.Text.Length > 0).ToArray();
    }

    /// <summary>Finds text or pictures repeated inside one test, across Parts as well.</summary>
    public static IReadOnlyList<string> FindDuplicatesInTest(IEnumerable<SpeakingSlotContent> slots)
    {
        var problems = new List<string>();
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            foreach (var unit in Units(slot.Content))
            {
                var label = slot.Label + unit.Suffix;
                if (!texts.TryAdd(unit.Text, label))
                    problems.Add($"{label} trùng nội dung với {texts[unit.Text]}.");
            }
            if (slot.ImageKey is { } imageKey && !images.TryAdd(imageKey, slot.Label))
                problems.Add($"{slot.Label} dùng cùng ảnh với {images[imageKey]}.");
        }
        return problems;
    }

    /// <summary>
    /// Compares new content with every other exercise in the bank. Text and pictures an edited exercise already
    /// had are skipped, so older duplicates do not stop an unrelated edit.
    /// </summary>
    public static IReadOnlyList<SpeakingContentFailure> FindBankDuplicates(SpeakingContentDto content,
        string? imageSha256, IReadOnlyList<SpeakingExerciseContentSource> bank, Guid? excludingExerciseId = null)
    {
        var own = bank.FirstOrDefault(source => source.ExerciseId == excludingExerciseId);
        var ownTexts = own is null ? [] : TextsOf(own).ToHashSet(StringComparer.Ordinal);
        var knownTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownImages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in bank.Where(source => source.ExerciseId != excludingExerciseId))
        {
            foreach (var text in TextsOf(source))
                knownTexts.TryAdd(text, source.Code);
            if (source.ImageSha256 is { } sha256)
                knownImages.TryAdd(sha256, source.Code);
        }

        var failures = new List<SpeakingContentFailure>();
        foreach (var unit in Units(content).Where(unit => !ownTexts.Contains(unit.Text)))
            if (knownTexts.TryGetValue(unit.Text, out var code))
                failures.Add(new(unit.Field, $"Nội dung này đã có trong bài {code}."));
        if (imageSha256 is not null && imageSha256 != own?.ImageSha256 &&
            knownImages.TryGetValue(imageSha256, out var imageCode))
            failures.Add(new(nameof(SpeakingExerciseInput.ImageAssetId), $"Ảnh này đã được dùng trong bài {imageCode}."));
        return failures;
    }

    private static IEnumerable<string> TextsOf(SpeakingExerciseContentSource source) =>
        Units(SpeakingJson.Deserialize<SpeakingContentDto>(source.ContentJson)).Select(unit => unit.Text);
}
