using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Application.Knowledge;

public sealed record ProcessedKnowledgeContent(
    string ContentJson,
    string TocJson,
    IReadOnlyList<KnowledgeTocItemDto> Toc,
    IReadOnlyList<Guid> EmbeddedMediaAssetIds,
    int EstimatedReadMinutes);

public static partial class KnowledgeContentProcessor
{
    public const int MaxContentBytes = 512 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ProcessedKnowledgeContent Process(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object)
            throw InvalidContent("Article content must be a valid JSON object (Tiptap document).");

        if (!content.TryGetProperty("type", out var typeProp) ||
            typeProp.ValueKind != JsonValueKind.String ||
            !string.Equals(typeProp.GetString(), "doc", StringComparison.Ordinal))
        {
            throw InvalidContent("Article content must have a root node with type = \"doc\".");
        }

        var rawJson = content.GetRawText();
        if (Encoding.UTF8.GetByteCount(rawJson) > MaxContentBytes)
            throw InvalidContent("Article content exceeds the maximum size of 512 KiB.");

        var toc = new List<KnowledgeTocItemDto>();
        var usedHeadingIds = new HashSet<string>(StringComparer.Ordinal);
        var headingIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var embeddedMediaIds = new HashSet<Guid>();
        var wordCount = 0;

        WalkNode(content, toc, usedHeadingIds, headingIdCounts, embeddedMediaIds, ref wordCount, depth: 0);

        var tocJson = JsonSerializer.Serialize(toc, SerializerOptions);
        var estimatedMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));
        return new ProcessedKnowledgeContent(rawJson, tocJson, toc, embeddedMediaIds.ToArray(), estimatedMinutes);
    }

    public static IReadOnlyList<KnowledgeTocItemDto> DeserializeToc(string tocJson)
    {
        if (string.IsNullOrWhiteSpace(tocJson))
            return [];
        return JsonSerializer.Deserialize<List<KnowledgeTocItemDto>>(tocJson, SerializerOptions) ?? [];
    }

    public static JsonElement ParseContent(string contentJson)
    {
        using var document = JsonDocument.Parse(contentJson);
        return document.RootElement.Clone();
    }

    private static void WalkNode(JsonElement node, List<KnowledgeTocItemDto> toc,
        HashSet<string> usedHeadingIds, Dictionary<string, int> headingIdCounts,
        HashSet<Guid> embeddedMediaIds, ref int wordCount, int depth)
    {
        if (depth > 32)
            throw InvalidContent("Content structure is nested too deeply.");
        if (node.ValueKind != JsonValueKind.Object)
            return;

        var nodeType = ReadType(node);

        if (node.TryGetProperty("marks", out var marksProp) && marksProp.ValueKind == JsonValueKind.Array)
            ValidateMarks(marksProp);

        if (string.Equals(nodeType, "text", StringComparison.Ordinal) &&
            node.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
        {
            wordCount += CountWords(textProp.GetString());
        }
        else if (string.Equals(nodeType, "heading", StringComparison.Ordinal))
        {
            var level = 2;
            if (node.TryGetProperty("attrs", out var attrsProp) && attrsProp.ValueKind == JsonValueKind.Object &&
                attrsProp.TryGetProperty("level", out var levelProp) && levelProp.ValueKind == JsonValueKind.Number &&
                levelProp.TryGetInt32(out var parsedLevel))
            {
                level = Math.Clamp(parsedLevel, 1, 6);
            }

            var headingText = ExtractPlainText(node).Trim();
            if (headingText.Length > 0 && level <= 4)
            {
                var baseId = KnowledgeSlug.Generate(headingText, 80);
                if (baseId.Length == 0)
                    baseId = $"section-{toc.Count + 1}";

                var count = headingIdCounts.GetValueOrDefault(baseId);
                string id;
                do
                {
                    count++;
                    id = count == 1 ? baseId : $"{baseId}-{count}";
                } while (!usedHeadingIds.Add(id));

                headingIdCounts[baseId] = count;
                toc.Add(new KnowledgeTocItemDto(id, level, headingText));
            }
        }
        else if (string.Equals(nodeType, "image", StringComparison.Ordinal) &&
                 node.TryGetProperty("attrs", out var imgAttrs) && imgAttrs.ValueKind == JsonValueKind.Object)
        {
            ExtractImageAssetId(imgAttrs, embeddedMediaIds);
        }

        if (node.TryGetProperty("content", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
                WalkNode(child, toc, usedHeadingIds, headingIdCounts, embeddedMediaIds, ref wordCount, depth + 1);
        }
    }

    private static string? ReadType(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var type))
            return null;
        if (type.ValueKind != JsonValueKind.String)
            throw InvalidContent("The type property in article content must be a string.");

        return type.GetString();
    }

    private static void ValidateMarks(JsonElement marks)
    {
        foreach (var mark in marks.EnumerateArray())
        {
            if (mark.ValueKind != JsonValueKind.Object)
                continue;
            if (!string.Equals(ReadType(mark), "link", StringComparison.Ordinal))
                continue;
            if (!mark.TryGetProperty("attrs", out var attrs) || attrs.ValueKind != JsonValueKind.Object)
                continue;
            if (!attrs.TryGetProperty("href", out var hrefProp) || hrefProp.ValueKind != JsonValueKind.String)
                continue;

            var href = hrefProp.GetString()?.Trim();
            if (string.IsNullOrEmpty(href))
                continue;

            var isSafe = href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                         (href.StartsWith("/", StringComparison.Ordinal) &&
                          !href.StartsWith("//", StringComparison.Ordinal) &&
                          !href.StartsWith("/\\", StringComparison.Ordinal)) ||
                         href.StartsWith("#", StringComparison.Ordinal);
            if (!isSafe)
                throw InvalidContent("Links in articles only accept http, https, mailto, or internal paths.");
        }
    }

    private static void ExtractImageAssetId(JsonElement attrs, HashSet<Guid> embeddedMediaIds)
    {
        if (attrs.TryGetProperty("assetId", out var assetIdProp) && assetIdProp.ValueKind == JsonValueKind.String &&
            Guid.TryParse(assetIdProp.GetString(), out var explicitId) && explicitId != Guid.Empty)
        {
            embeddedMediaIds.Add(explicitId);
            return;
        }

        if (attrs.TryGetProperty("src", out var srcProp) && srcProp.ValueKind == JsonValueKind.String)
        {
            var src = srcProp.GetString();
            if (!string.IsNullOrWhiteSpace(src))
            {
                var match = MediaUrlAssetIdRegex().Match(src);
                if (match.Success && Guid.TryParse(match.Groups[1].Value, out var urlAssetId) &&
                    urlAssetId != Guid.Empty)
                {
                    embeddedMediaIds.Add(urlAssetId);
                }
            }
        }
    }

    private static string ExtractPlainText(JsonElement node)
    {
        var sb = new StringBuilder();
        CollectText(node, sb);
        return sb.ToString();
    }

    private static void CollectText(JsonElement node, StringBuilder sb)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;
        if (string.Equals(ReadType(node), "text", StringComparison.Ordinal) &&
            node.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
        {
            sb.Append(textProp.GetString());
        }

        if (node.TryGetProperty("content", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
                CollectText(child, sb);
        }
    }

    private static InvalidKnowledgeContentException InvalidContent(string message) => new(message, "content");

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return WordRegex().Matches(text).Count;
    }

    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"/knowledge/media/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MediaUrlAssetIdRegex();
}
