using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Importing;

public sealed record ImportedExamWorkbook(string TestCode, string Title, Guid ScoreProfileId,
    IReadOnlyList<ImportedExamBlock> Blocks, IReadOnlyList<ImportedExamContext> Contexts,
    IReadOnlyList<ImportedExamQuestion> Questions);

public sealed record ImportedExamBlock(string Key, int Part, ExamBlockType Type, int FirstQuestionNumber,
    int QuestionCount);

public sealed record ImportedExamContext(string BlockKey, int Ordinal, ExamContextType Type, string? Text,
    Guid? ImageAssetId, string? AltText);

public sealed record ImportedExamQuestion(string BlockKey, int QuestionNumber, ExamQuestionContent Content);

public sealed class ExamWorkbookFormatException(IReadOnlyList<string> errors) : Exception
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public static class XlsxExamWorkbookReader
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private const int MaxEntries = 64;
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static ImportedExamWorkbook Read(byte[] content)
    {
        if (content.Length == 0 || content.Length > MaxBytes || content.Length < 4 ||
            content[0] != (byte)'P' || content[1] != (byte)'K')
            throw new ExamWorkbookFormatException(["Workbook phải là tệp XLSX hợp lệ, tối đa 10 MiB."]);

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > MaxEntries || archive.Entries.Sum(entry => entry.Length) > MaxBytes)
                throw new ExamWorkbookFormatException(["Workbook vượt quá giới hạn cấu trúc an toàn."]);

            var sharedStrings = ReadSharedStrings(archive);
            var sheets = ReadSheets(archive, sharedStrings);
            var errors = new List<string>();
            var manifest = ReadManifest(RequireSheet(sheets, "Manifest", errors), errors);
            var blocks = ReadBlocks(RequireSheet(sheets, "Blocks", errors), errors);
            var contexts = ReadContexts(RequireSheet(sheets, "ContextBlocks", errors), errors);
            var questions = ReadQuestions(RequireSheet(sheets, "Questions", errors), errors);
            if (errors.Count > 0)
                throw new ExamWorkbookFormatException(errors);

            ValidateManifest(manifest, errors);
            var scoreProfileId = ParseGuid(manifest, "ScoreProfileId", "Manifest", errors);
            if (errors.Count > 0)
                throw new ExamWorkbookFormatException(errors);
            return new ImportedExamWorkbook(manifest["TestCode"], manifest["Title"], scoreProfileId, blocks, contexts,
                questions);
        }
        catch (ExamWorkbookFormatException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new ExamWorkbookFormatException(["Workbook XLSX bị hỏng hoặc không thể đọc."]);
        }
        catch (Exception exception) when (exception is FormatException or XmlException or OverflowException or
                                           ArgumentException or InvalidOperationException or NullReferenceException)
        {
            throw new ExamWorkbookFormatException(["Workbook XLSX có dữ liệu không hợp lệ."]);
        }
    }

    private static Dictionary<string, SheetData> ReadSheets(ZipArchive archive, IReadOnlyList<string> sharedStrings)
    {
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var relations = ReadXml(archive, "xl/_rels/workbook.xml.rels").Root!.Elements(PackageRelationships + "Relationship")
            .ToDictionary(element => (string)element.Attribute("Id")!, element => (string)element.Attribute("Target")!);
        var sheets = new Dictionary<string, SheetData>(StringComparer.Ordinal);
        foreach (var sheet in workbook.Root!.Element(Spreadsheet + "sheets")!.Elements(Spreadsheet + "sheet"))
        {
            var name = (string?)sheet.Attribute("name");
            var relationId = (string?)sheet.Attribute(OfficeRelationships + "id");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relationId) || !relations.TryGetValue(relationId, out var target))
                continue;
            var entryPath = target.StartsWith("/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target;
            sheets[name] = ReadSheet(archive, entryPath, name, sharedStrings);
        }
        return sheets;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];
        using var stream = entry.Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        return document.Root!.Elements(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value))).ToArray();
    }

    private static SheetData ReadSheet(ZipArchive archive, string entryPath, string name,
        IReadOnlyList<string> sharedStrings)
    {
        var document = ReadXml(archive, entryPath);
        var rows = new List<SheetRow>();
        var sheetData = document.Root!.Element(Spreadsheet + "sheetData");
        if (sheetData is null)
            return new SheetData(name, rows);
        foreach (var row in sheetData.Elements(Spreadsheet + "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                if (reference is null)
                    continue;
                var column = GetColumnIndex(reference);
                cells[column] = GetCellValue(cell, sharedStrings);
            }
            rows.Add(new SheetRow((int?)row.Attribute("r") ?? rows.Count + 1, cells));
        }
        return new SheetData(name, rows);
    }

    private static string GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));
        var value = cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count)
            return sharedStrings[index];
        return value;
    }

    private static XDocument ReadXml(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath) ?? throw new InvalidDataException();
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static int GetColumnIndex(string reference)
    {
        var value = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            value = checked(value * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        return value - 1;
    }

    private static SheetData RequireSheet(IReadOnlyDictionary<string, SheetData> sheets, string sheetName,
        ICollection<string> errors)
    {
        if (sheets.TryGetValue(sheetName, out var sheet))
            return sheet;
        errors.Add($"Thiếu sheet bắt buộc '{sheetName}'.");
        return new SheetData(sheetName, []);
    }

    private static Dictionary<string, string> ReadManifest(SheetData sheet, ICollection<string> errors)
    {
        var table = ReadTable(sheet, ["Field", "Value"], errors);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in table)
        {
            var field = row.Get("Field");
            var value = row.Get("Value");
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Manifest!A{row.RowNumber}: Field và Value là bắt buộc.");
                continue;
            }
            if (!result.TryAdd(field.Trim(), value.Trim()))
                errors.Add($"Manifest!A{row.RowNumber}: Field '{field}' bị lặp.");
        }
        return result;
    }

    private static List<ImportedExamBlock> ReadBlocks(SheetData sheet, ICollection<string> errors)
    {
        var table = ReadTable(sheet, ["BlockKey", "Part", "BlockType", "FirstQuestionNumber", "QuestionCount"], errors);
        var result = new List<ImportedExamBlock>();
        foreach (var row in table)
        {
            if (!TryParseInt(row, "Part", sheet.Name, errors, out var part) ||
                !TryParseInt(row, "FirstQuestionNumber", sheet.Name, errors, out var firstQuestion) ||
                !TryParseInt(row, "QuestionCount", sheet.Name, errors, out var questionCount))
                continue;
            if (!TryParseBlockType(row.Get("BlockType"), row, sheet.Name, errors, out var type))
                continue;
            var key = Required(row, "BlockKey", sheet.Name, errors);
            if (key is not null)
                result.Add(new ImportedExamBlock(key, part, type, firstQuestion, questionCount));
        }
        return result;
    }

    private static List<ImportedExamContext> ReadContexts(SheetData sheet, ICollection<string> errors)
    {
        var table = ReadTable(sheet, ["BlockKey", "Ordinal", "Type", "Text", "ImageAssetId", "AltText"], errors);
        var result = new List<ImportedExamContext>();
        foreach (var row in table)
        {
            if (!TryParseInt(row, "Ordinal", sheet.Name, errors, out var ordinal) ||
                !TryParseContextType(row.Get("Type"), row, sheet.Name, errors, out var type))
                continue;
            var key = Required(row, "BlockKey", sheet.Name, errors);
            if (key is null)
                continue;
            Guid? imageAssetId = null;
            var assetId = row.Get("ImageAssetId");
            if (!string.IsNullOrWhiteSpace(assetId))
            {
                if (Guid.TryParse(assetId, out var parsed))
                    imageAssetId = parsed;
                else
                    errors.Add($"{sheet.Name}!E{row.RowNumber}: ImageAssetId không phải UUID.");
            }
            result.Add(new ImportedExamContext(key, ordinal, type, EmptyToNull(row.Get("Text")), imageAssetId,
                EmptyToNull(row.Get("AltText"))));
        }
        return result;
    }

    private static List<ImportedExamQuestion> ReadQuestions(SheetData sheet, ICollection<string> errors)
    {
        var table = ReadTable(sheet, ["BlockKey", "QuestionNumber", "QuestionCode", "Stem", "OptionA", "OptionB",
            "OptionC", "OptionD", "CorrectKey", "Translation", "Explanation", "ImageAssetId"], errors);
        var result = new List<ImportedExamQuestion>();
        foreach (var row in table)
        {
            if (!TryParseInt(row, "QuestionNumber", sheet.Name, errors, out var questionNumber))
                continue;
            var blockKey = Required(row, "BlockKey", sheet.Name, errors);
            var code = Required(row, "QuestionCode", sheet.Name, errors);
            var stem = Required(row, "Stem", sheet.Name, errors);
            var optionA = Required(row, "OptionA", sheet.Name, errors);
            var optionB = Required(row, "OptionB", sheet.Name, errors);
            var optionC = Required(row, "OptionC", sheet.Name, errors);
            var correctKey = Required(row, "CorrectKey", sheet.Name, errors);
            Guid? imageAssetId = null;
            var assetId = row.Get("ImageAssetId");
            if (!string.IsNullOrWhiteSpace(assetId))
            {
                if (Guid.TryParse(assetId, out var parsed))
                    imageAssetId = parsed;
                else
                    errors.Add($"{sheet.Name}!L{row.RowNumber}: ImageAssetId không phải UUID.");
            }
            if (blockKey is null || code is null || stem is null || optionA is null || optionB is null || optionC is null ||
                correctKey is null)
                continue;
            result.Add(new ImportedExamQuestion(blockKey, questionNumber, new ExamQuestionContent(code, stem, optionA,
                optionB, optionC, EmptyToNull(row.Get("OptionD")), correctKey, EmptyToNull(row.Get("Translation")),
                EmptyToNull(row.Get("Explanation")), imageAssetId)));
        }
        return result;
    }

    private static IReadOnlyList<TableRow> ReadTable(SheetData sheet, IReadOnlyList<string> expectedHeaders,
        ICollection<string> errors)
    {
        var header = sheet.Rows.FirstOrDefault(row => expectedHeaders.All(expected => row.Cells.Values.Any(value =>
            string.Equals(value.Trim(), expected, StringComparison.Ordinal))));
        if (header is null)
        {
            errors.Add($"Sheet '{sheet.Name}' thiếu header yêu cầu.");
            return [];
        }
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var headerName in expectedHeaders)
        {
            var matches = header.Cells.Where(cell => string.Equals(cell.Value.Trim(), headerName,
                StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                errors.Add($"Sheet '{sheet.Name}' có header '{headerName}' bị lặp hoặc không xác định.");
                continue;
            }
            columns[headerName] = matches[0].Key;
        }
        if (columns.Count != expectedHeaders.Count)
            return [];
        return sheet.Rows.Where(row => row.RowNumber > header.RowNumber && row.Cells.Values.Any(value =>
                !string.IsNullOrWhiteSpace(value))).Select(row => new TableRow(row.RowNumber, columns.ToDictionary(
                column => column.Key, column => row.Cells.TryGetValue(column.Value, out var value) ? value.Trim() : string.Empty,
                StringComparer.Ordinal))).ToArray();
    }

    private static void ValidateManifest(IReadOnlyDictionary<string, string> manifest, ICollection<string> errors)
    {
        foreach (var required in new[] { "SchemaVersion", "TestCode", "Title", "Scope", "ScoreProfileId" })
            if (!manifest.ContainsKey(required))
                errors.Add($"Manifest thiếu '{required}'.");
        if (manifest.TryGetValue("SchemaVersion", out var schemaVersion) && schemaVersion != "1")
            errors.Add("Manifest!B: SchemaVersion phải là 1.");
        if (manifest.TryGetValue("Scope", out var scope) && scope != "LISTENING_READING")
            errors.Add("Manifest!B: Scope phải là LISTENING_READING.");
    }

    private static Guid ParseGuid(IReadOnlyDictionary<string, string> values, string name, string sheetName,
        ICollection<string> errors)
    {
        if (values.TryGetValue(name, out var value) && Guid.TryParse(value, out var id) && id != Guid.Empty)
            return id;
        errors.Add($"{sheetName}: '{name}' phải là UUID hợp lệ.");
        return Guid.Empty;
    }

    private static bool TryParseInt(TableRow row, string field, string sheetName, ICollection<string> errors,
        out int value)
    {
        if (int.TryParse(row.Get(field), out value))
            return true;
        errors.Add($"{sheetName}!{field}{row.RowNumber}: phải là số nguyên.");
        return false;
    }

    private static bool TryParseBlockType(string value, TableRow row, string sheetName, ICollection<string> errors,
        out ExamBlockType type)
    {
        if (string.Equals(value, "SINGLE", StringComparison.OrdinalIgnoreCase))
        {
            type = ExamBlockType.Single;
            return true;
        }
        if (string.Equals(value, "GROUP", StringComparison.OrdinalIgnoreCase))
        {
            type = ExamBlockType.Group;
            return true;
        }
        errors.Add($"{sheetName}!BlockType{row.RowNumber}: chỉ nhận SINGLE hoặc GROUP.");
        type = default;
        return false;
    }

    private static bool TryParseContextType(string value, TableRow row, string sheetName, ICollection<string> errors,
        out ExamContextType type)
    {
        if (string.Equals(value, "TEXT", StringComparison.OrdinalIgnoreCase))
        {
            type = ExamContextType.Text;
            return true;
        }
        if (string.Equals(value, "IMAGE", StringComparison.OrdinalIgnoreCase))
        {
            type = ExamContextType.Image;
            return true;
        }
        errors.Add($"{sheetName}!Type{row.RowNumber}: chỉ nhận TEXT hoặc IMAGE.");
        type = default;
        return false;
    }

    private static string? Required(TableRow row, string field, string sheetName, ICollection<string> errors)
    {
        var value = row.Get(field);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        errors.Add($"{sheetName}!{field}{row.RowNumber}: là bắt buộc.");
        return null;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record SheetData(string Name, IReadOnlyList<SheetRow> Rows);
    private sealed record SheetRow(int RowNumber, IReadOnlyDictionary<int, string> Cells);
    private sealed record TableRow(int RowNumber, IReadOnlyDictionary<string, string> Values)
    {
        public string Get(string columnName) => Values.TryGetValue(columnName, out var value) ? value : string.Empty;
    }
}
