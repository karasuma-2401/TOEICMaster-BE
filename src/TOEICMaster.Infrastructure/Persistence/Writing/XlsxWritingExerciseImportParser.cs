using System.IO.Compression;
using System.Xml.Linq;
using TOEICMaster.Application.Writing.Admin;

namespace TOEICMaster.Infrastructure.Persistence.Writing;

public sealed class XlsxWritingExerciseImportParser : IWritingExerciseImportParser
{
    private const int MaximumRows = 5_000;

    private static readonly IReadOnlyDictionary<int, string[]> RequiredHeaders = new Dictionary<int, string[]>
    {
        [1] = ["Code", "Topic", "Difficulty", "Status", "ImageAssetId", "ImageAlt", "KeywordOne", "KeywordTwo", "ModelAnswer"],
        [2] = ["Code", "Topic", "Difficulty", "Status", "Sender", "Recipient", "Subject", "EmailBody", "Directions", "ModelAnswer"],
        [3] = ["Code", "Topic", "Difficulty", "Status", "Prompt", "MinimumWordCount", "ModelAnswer"]
    };

    public WritingExerciseImportPreviewDto Preview(int part, string fileName, byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var workbook = archive.Entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) &&
                                                           entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.FullName, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new InvalidWritingImportException("File .xlsx không có worksheet.");
            var sharedStrings = ReadSharedStrings(archive);
            using var worksheetStream = workbook.Open();
            var worksheet = XDocument.Load(worksheetStream);
            var rows = ReadRows(worksheet, sharedStrings).ToArray();
            if (rows.Length == 0)
                throw new InvalidWritingImportException("File .xlsx chưa có dòng tiêu đề.");
            var headers = rows[0].Cells.ToDictionary(pair => pair.Key, pair => pair.Value.Trim(),
                EqualityComparer<int>.Default);
            var headerIndexes = headers.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var missing = RequiredHeaders[part].Where(header => !headerIndexes.ContainsKey(header)).ToArray();
            var duplicated = headerIndexes.Where(pair => pair.Value.Length > 1).Select(pair => pair.Key).ToArray();
            if (missing.Length > 0 || duplicated.Length > 0)
            {
                var errors = new List<string>();
                if (missing.Length > 0)
                    errors.Add($"Thiếu cột: {string.Join(", ", missing)}.");
                if (duplicated.Length > 0)
                    errors.Add($"Trùng cột: {string.Join(", ", duplicated)}.");
                return new WritingExerciseImportPreviewDto(part, fileName,
                    [new WritingExerciseImportRowDto(rows[0].RowNumber, null, errors)]);
            }
            var validHeaders = headerIndexes.ToDictionary(pair => pair.Key, pair => pair.Value[0],
                StringComparer.OrdinalIgnoreCase);
            var dataRows = rows.Skip(1).Where(row => row.Cells.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                .Take(MaximumRows + 1).ToArray();
            if (dataRows.Length > MaximumRows)
                throw new InvalidWritingImportException($"File import chỉ hỗ trợ tối đa {MaximumRows} dòng dữ liệu.");
            return new WritingExerciseImportPreviewDto(part, fileName,
                dataRows.Select(row => ValidateRow(part, row, validHeaders)).ToArray());
        }
        catch (InvalidWritingImportException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new InvalidWritingImportException("File import không phải .xlsx hợp lệ.");
        }
        catch (System.Xml.XmlException)
        {
            throw new InvalidWritingImportException("Nội dung .xlsx không hợp lệ.");
        }
    }

    private static WritingExerciseImportRowDto ValidateRow(int part, SpreadsheetRow row,
        IReadOnlyDictionary<string, int> headers)
    {
        string Value(string header) => row.Cells.GetValueOrDefault(headers[header])?.Trim() ?? string.Empty;
        var code = Value("Code");
        var errors = new List<string>();
        foreach (var header in RequiredHeaders[part])
        {
            if (string.IsNullOrWhiteSpace(Value(header)))
                errors.Add($"Thiếu {header}.");
        }
        if (!string.IsNullOrWhiteSpace(code) && code.Length > 64)
            errors.Add("Code không được vượt quá 64 ký tự.");
        if (!string.IsNullOrWhiteSpace(Value("Topic")) && Value("Topic").Length > 100)
            errors.Add("Topic không được vượt quá 100 ký tự.");
        if (!string.IsNullOrWhiteSpace(Value("Difficulty")) &&
            new[] { "EASY", "MEDIUM", "HARD" }.Contains(Value("Difficulty"), StringComparer.OrdinalIgnoreCase) is false)
            errors.Add("Difficulty phải là EASY, MEDIUM hoặc HARD.");
        if (!string.IsNullOrWhiteSpace(Value("Status")) &&
            new[] { "DRAFT", "PUBLISHED", "ARCHIVED" }.Contains(Value("Status"), StringComparer.OrdinalIgnoreCase) is false)
            errors.Add("Status phải là DRAFT, PUBLISHED hoặc ARCHIVED.");
        if (part == 1 && !Guid.TryParse(Value("ImageAssetId"), out _))
            errors.Add("ImageAssetId phải là UUID của ảnh đã upload.");
        if (part == 3 && (!int.TryParse(Value("MinimumWordCount"), out var minimum) || minimum is < 1 or > 5_000))
            errors.Add("MinimumWordCount phải là số từ 1 đến 5000.");
        return new WritingExerciseImportRowDto(row.RowNumber, string.IsNullOrWhiteSpace(code) ? null : code, errors);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants().Where(element => element.Name.LocalName == "si")
            .Select(element => string.Concat(element.Descendants().Where(child => child.Name.LocalName == "t")
                .Select(child => child.Value))).ToArray();
    }

    private static IEnumerable<SpreadsheetRow> ReadRows(XDocument document, IReadOnlyList<string> sharedStrings)
    {
        foreach (var row in document.Descendants().Where(element => element.Name.LocalName == "row"))
        {
            var number = int.TryParse(row.Attribute("r")?.Value, out var parsedNumber) ? parsedNumber : 0;
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                if (reference is null || !TryGetColumnIndex(reference, out var column))
                    continue;
                cells[column] = ReadCellValue(cell, sharedStrings);
            }
            yield return new SpreadsheetRow(number, cells);
        }
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
            return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t")
                .Select(element => element.Value));
        var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count)
            return sharedStrings[index];
        return raw;
    }

    private static bool TryGetColumnIndex(string reference, out int index)
    {
        index = 0;
        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character))
                break;
            index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return index > 0;
    }

    private sealed record SpreadsheetRow(int RowNumber, IReadOnlyDictionary<int, string> Cells);
}

