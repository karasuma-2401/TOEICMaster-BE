using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using TOEICMaster.Application.ExerciseBank;

namespace TOEICMaster.Infrastructure.Persistence.ExerciseBank;

public sealed class XlsxLrExerciseBankImportParser : ILrExerciseBankImportParser
{
    private const int MaximumRows = 5_000;
    private const int MaximumArchiveEntries = 64;
    private const long MaximumArchiveEntryBytes = 16L * 1024 * 1024;
    private const long MaximumArchiveBytes = 24L * 1024 * 1024;
    private static readonly string[] SingleHeaders = ["Code", "Topic", "Difficulty", "Status", "Prompt", "OptionA",
        "OptionB", "OptionC", "OptionD", "CorrectKey", "AudioUrl", "ImageUrl", "Translation", "Explanation"];
    private static readonly string[] GroupHeaders = ["GroupCode", "Topic", "Difficulty", "Status", "Context", "AudioUrl",
        "ImageUrl", "QuestionCode", "Prompt", "OptionA", "OptionB", "OptionC", "OptionD", "CorrectKey", "Translation",
        "Explanation"];

    public IReadOnlyList<LrExerciseItemInput> Parse(int part, string fileName, byte[] content)
    {
        if (part is < 1 or > 7)
            throw new LrExerciseBankImportException("Part import không hợp lệ.");
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new LrExerciseBankImportException("Chỉ hỗ trợ file Excel .xlsx.");
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            ValidateArchive(archive);
            var worksheetEntry = archive.Entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) &&
                entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.FullName,
                StringComparer.Ordinal).FirstOrDefault() ?? throw new LrExerciseBankImportException("Workbook không có worksheet.");
            var sharedStrings = ReadSharedStrings(archive);
            var worksheet = LoadXml(worksheetEntry);
            var rows = ReadRows(worksheet, sharedStrings).ToArray();
            if (rows.Length == 0)
                throw new LrExerciseBankImportException("Workbook chưa có dòng tiêu đề.");
            var headers = ReadHeaders(rows[0], IsGrouped(part) ? GroupHeaders : SingleHeaders);
            var dataRows = rows.Skip(1).Where(row => row.Cells.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                .Take(MaximumRows + 1).ToArray();
            if (dataRows.Length > MaximumRows)
                throw new LrExerciseBankImportException($"Workbook chỉ hỗ trợ tối đa {MaximumRows} dòng dữ liệu.");
            return IsGrouped(part) ? ParseGroups(dataRows, headers) : ParseSingles(dataRows, headers);
        }
        catch (LrExerciseBankImportException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new LrExerciseBankImportException("File không phải workbook .xlsx hợp lệ.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new LrExerciseBankImportException("Không thể đọc workbook import: " + exception.Message);
        }
    }

    private static IReadOnlyList<LrExerciseItemInput> ParseSingles(IReadOnlyList<SpreadsheetRow> rows,
        IReadOnlyDictionary<string, int> headers)
    {
        var errors = new List<string>();
        var items = new List<LrExerciseItemInput>();
        foreach (var row in rows)
        {
            var question = ToQuestion(row, headers, false);
            var code = Cell(row, headers, "Code");
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add($"Dòng {row.RowNumber}: Code là bắt buộc.");
                continue;
            }
            items.Add(new LrExerciseItemInput(code, Cell(row, headers, "Topic"), Cell(row, headers, "Difficulty"),
                Cell(row, headers, "Status"), null, NullIfBlank(Cell(row, headers, "AudioUrl")),
                NullIfBlank(Cell(row, headers, "ImageUrl")), [question]));
        }
        ThrowIfErrors(errors);
        return items;
    }

    private static IReadOnlyList<LrExerciseItemInput> ParseGroups(IReadOnlyList<SpreadsheetRow> rows,
        IReadOnlyDictionary<string, int> headers)
    {
        var errors = new List<string>();
        var groups = new Dictionary<string, List<SpreadsheetRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var groupCode = Cell(row, headers, "GroupCode");
            if (string.IsNullOrWhiteSpace(groupCode))
            {
                errors.Add($"Dòng {row.RowNumber}: GroupCode là bắt buộc.");
                continue;
            }
            if (!groups.TryGetValue(groupCode.Trim(), out var group))
                groups[groupCode.Trim()] = group = [];
            group.Add(row);
        }
        var items = new List<LrExerciseItemInput>();
        foreach (var (groupCode, groupRows) in groups)
        {
            var first = groupRows[0];
            ValidateSharedGroupFields(groupRows, headers, errors);
            var questions = groupRows.Select(row => ToQuestion(row, headers, true)).ToArray();
            items.Add(new LrExerciseItemInput(groupCode, Cell(first, headers, "Topic"), Cell(first, headers, "Difficulty"),
                Cell(first, headers, "Status"), Cell(first, headers, "Context"), NullIfBlank(Cell(first, headers, "AudioUrl")),
                NullIfBlank(Cell(first, headers, "ImageUrl")), questions));
        }
        ThrowIfErrors(errors);
        return items;
    }

    private static LrExerciseQuestionInput ToQuestion(SpreadsheetRow row, IReadOnlyDictionary<string, int> headers,
        bool grouped)
    {
        var code = Cell(row, headers, grouped ? "QuestionCode" : "Code");
        return new LrExerciseQuestionInput(code, Cell(row, headers, "Prompt"), Cell(row, headers, "OptionA"),
            Cell(row, headers, "OptionB"), Cell(row, headers, "OptionC"), NullIfBlank(Cell(row, headers, "OptionD")),
            Cell(row, headers, "CorrectKey"), NullIfBlank(Cell(row, headers, "Translation")),
            NullIfBlank(Cell(row, headers, "Explanation")));
    }

    private static IReadOnlyDictionary<string, int> ReadHeaders(SpreadsheetRow header, IReadOnlyCollection<string> required)
    {
        var result = header.Cells.Where(cell => !string.IsNullOrWhiteSpace(cell.Value))
            .GroupBy(cell => cell.Value.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key,
                group => group.Select(cell => cell.Key).ToArray(), StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var missing = required.Where(name => !result.ContainsKey(name)).ToArray();
        var duplicated = result.Where(pair => pair.Value.Length > 1).Select(pair => pair.Key).ToArray();
        if (missing.Length > 0)
            errors.Add("Thiếu cột: " + string.Join(", ", missing) + ".");
        if (duplicated.Length > 0)
            errors.Add("Trùng cột: " + string.Join(", ", duplicated) + ".");
        ThrowIfErrors(errors);
        return result.ToDictionary(pair => pair.Key, pair => pair.Value[0], StringComparer.OrdinalIgnoreCase);
    }

    private static string Cell(SpreadsheetRow row, IReadOnlyDictionary<string, int> headers, string name) =>
        row.Cells.TryGetValue(headers[name], out var value) ? value.Trim() : string.Empty;

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool IsGrouped(int part) => part is 3 or 4 or 6 or 7;

    private static void ThrowIfErrors(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0)
            throw new LrExerciseBankImportException(string.Join(" ", errors.Take(20)));
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new LrExerciseBankImportException("Workbook has too many entries for an import template.");
        long uncompressedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaximumArchiveEntryBytes)
                throw new LrExerciseBankImportException("Workbook contains an entry that is too large.");
            try
            {
                uncompressedBytes = checked(uncompressedBytes + entry.Length);
            }
            catch (OverflowException)
            {
                throw new LrExerciseBankImportException("Workbook has an invalid size.");
            }
            if (uncompressedBytes > MaximumArchiveBytes)
                throw new LrExerciseBankImportException("Workbook exceeds the decompressed-size limit.");
        }
    }

    private static void ValidateSharedGroupFields(IReadOnlyList<SpreadsheetRow> rows,
        IReadOnlyDictionary<string, int> headers, ICollection<string> errors)
    {
        var first = rows[0];
        foreach (var row in rows.Skip(1))
        {
            foreach (var field in new[] { "Topic", "Difficulty", "Status", "Context", "AudioUrl", "ImageUrl" })
            {
                var expected = Cell(first, headers, field);
                var actual = Cell(row, headers, field);
                if (!string.IsNullOrWhiteSpace(actual) && !string.Equals(expected, actual, StringComparison.Ordinal))
                    errors.Add($"Row {row.RowNumber}: {field} must match the first row of its GroupCode or be blank.");
            }
        }
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];
        var document = LoadXml(entry);
        return document.Descendants().Where(element => element.Name.LocalName == "si").Select(element => string.Concat(
            element.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value))).ToArray();
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumArchiveEntryBytes
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static IEnumerable<SpreadsheetRow> ReadRows(XDocument worksheet, IReadOnlyList<string> sharedStrings)
    {
        foreach (var row in worksheet.Descendants().Where(element => element.Name.LocalName == "row"))
        {
            var rowNumber = int.TryParse(row.Attribute("r")?.Value, out var parsed) ? parsed : 0;
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
            {
                if (!TryGetColumnIndex(cell.Attribute("r")?.Value, out var column))
                    continue;
                cells[column] = ReadCellValue(cell, sharedStrings);
            }
            yield return new SpreadsheetRow(rowNumber, cells);
        }
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
            return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
        var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? string.Empty;
        return type == "s" && int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : raw;
    }

    private static bool TryGetColumnIndex(string? reference, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(reference))
            return false;
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
