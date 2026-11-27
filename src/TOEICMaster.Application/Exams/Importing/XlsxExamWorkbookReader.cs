using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using TOEICMaster.Application.Common.Xml;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Importing;

public sealed record ImportedExamWorkbook(string TestCode, string Title, Guid ScoreProfileId,
    IReadOnlyList<ImportedExamBlock> Blocks, IReadOnlyList<ImportedExamContext> Contexts,
    IReadOnlyList<ImportedExamQuestion> Questions);

public sealed record ImportedExamBlock(string Key, int Part, ExamBlockType Type, int FirstQuestionNumber,
    int QuestionCount);

public sealed record ImportedExamContext(string BlockKey, int Ordinal, ExamContextType Type, string? Text,
    Guid? ImageAssetId, string? AltText, bool ImageRequired = false);

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
            throw new ExamWorkbookFormatException(["Workbook must be a valid XLSX file of at most 10 MiB."]);

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > MaxEntries || archive.Entries.Sum(entry => entry.Length) > MaxBytes)
                throw new ExamWorkbookFormatException(["Workbook exceeds safe structural limits."]);

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
            throw new ExamWorkbookFormatException(["XLSX workbook is corrupted or unreadable."]);
        }
        catch (Exception exception) when (exception is FormatException or XmlException or OverflowException or
                                           ArgumentException or InvalidOperationException or NullReferenceException)
        {
            throw new ExamWorkbookFormatException(["XLSX workbook contains invalid data."]);
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
        var document = SafeXmlLoader.Load(stream);
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
        return SafeXmlLoader.Load(stream);
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
        errors.Add($"Missing required sheet '{sheetName}'.");
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
                errors.Add($"Manifest!A{row.RowNumber}: Field and Value are required.");
                continue;
            }
            if (!result.TryAdd(field.Trim(), value.Trim()))
                errors.Add($"Manifest!A{row.RowNumber}: Duplicate Field '{field}'.");
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
        var table = ReadTable(sheet, ["BlockKey", "Ordinal", "Type", "Text", "ImageAssetId", "AltText"], errors,
            ["ImageRequired"]);
        var result = new List<ImportedExamContext>();
        foreach (var row in table)
        {
            if (!TryParseInt(row, "Ordinal", sheet.Name, errors, out var ordinal) ||
                !TryParseContextType(row.Get("Type"), row, sheet.Name, errors, out var type))
                continue;
            var key = Required(row, "BlockKey", sheet.Name, errors);
            if (key is null)
                continue;
            if (!TryParseBoolean(row, "ImageRequired", sheet.Name, errors, out var imageRequired))
                continue;
            Guid? imageAssetId = null;
            var assetId = row.Get("ImageAssetId");
            if (!string.IsNullOrWhiteSpace(assetId))
            {
                if (Guid.TryParse(assetId, out var parsed))
                    imageAssetId = parsed;
                else
                    errors.Add($"{sheet.Name}!E{row.RowNumber}: ImageAssetId is not a valid UUID.");
            }
            result.Add(new ImportedExamContext(key, ordinal, type, EmptyToNull(row.Get("Text")), imageAssetId,
                EmptyToNull(row.Get("AltText")), imageRequired));
        }
        return result;
    }

    private static List<ImportedExamQuestion> ReadQuestions(SheetData sheet, ICollection<string> errors)
    {
        var table = ReadTable(sheet, ["BlockKey", "QuestionNumber", "QuestionCode", "Stem", "OptionA", "OptionB",
            "OptionC", "OptionD", "CorrectKey", "Translation", "Explanation", "ImageAssetId"], errors,
            ["ImageRequired"]);
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
            if (!TryParseBoolean(row, "ImageRequired", sheet.Name, errors, out var imageRequired))
                continue;
            Guid? imageAssetId = null;
            var assetId = row.Get("ImageAssetId");
            if (!string.IsNullOrWhiteSpace(assetId))
            {
                if (Guid.TryParse(assetId, out var parsed))
                    imageAssetId = parsed;
                else
                    errors.Add($"{sheet.Name}!L{row.RowNumber}: ImageAssetId is not a valid UUID.");
            }
            if (blockKey is null || code is null || stem is null || optionA is null || optionB is null || optionC is null ||
                correctKey is null)
                continue;
            result.Add(new ImportedExamQuestion(blockKey, questionNumber, new ExamQuestionContent(code, stem, optionA,
                optionB, optionC, EmptyToNull(row.Get("OptionD")), correctKey, EmptyToNull(row.Get("Translation")),
                EmptyToNull(row.Get("Explanation")), imageAssetId, imageRequired)));
        }
        return result;
    }

    private static IReadOnlyList<TableRow> ReadTable(SheetData sheet, IReadOnlyList<string> expectedHeaders,
        ICollection<string> errors, IReadOnlyList<string>? optionalHeaders = null)
    {
        var header = sheet.Rows.FirstOrDefault(row => expectedHeaders.All(expected => row.Cells.Values.Any(value =>
            string.Equals(value.Trim(), expected, StringComparison.Ordinal))));
        if (header is null)
        {
            errors.Add($"Sheet '{sheet.Name}' is missing required headers.");
            return [];
        }
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var headerName in expectedHeaders)
        {
            var matches = header.Cells.Where(cell => string.Equals(cell.Value.Trim(), headerName,
                StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                errors.Add($"Sheet '{sheet.Name}' has duplicate or missing header '{headerName}'.");
                continue;
            }
            columns[headerName] = matches[0].Key;
        }
        if (optionalHeaders is not null)
            foreach (var headerName in optionalHeaders)
            {
                var matches = header.Cells.Where(cell => string.Equals(cell.Value.Trim(), headerName,
                    StringComparison.Ordinal)).ToArray();
                if (matches.Length > 1)
                    errors.Add($"Sheet '{sheet.Name}' has duplicate header '{headerName}'.");
                else if (matches.Length == 1)
                    columns[headerName] = matches[0].Key;
            }
        if (expectedHeaders.Any(headerName => !columns.ContainsKey(headerName)))
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
                errors.Add($"Manifest is missing '{required}'.");
        if (manifest.TryGetValue("SchemaVersion", out var schemaVersion) && schemaVersion != "1")
            errors.Add("Manifest!B: SchemaVersion must be 1.");
        if (manifest.TryGetValue("Scope", out var scope) && scope != "LISTENING_READING")
            errors.Add("Manifest!B: Scope must be LISTENING_READING.");
    }

    private static Guid ParseGuid(IReadOnlyDictionary<string, string> values, string name, string sheetName,
        ICollection<string> errors)
    {
        if (values.TryGetValue(name, out var value) && Guid.TryParse(value, out var id) && id != Guid.Empty)
            return id;
        errors.Add($"{sheetName}: '{name}' must be a valid UUID.");
        return Guid.Empty;
    }

    private static bool TryParseInt(TableRow row, string field, string sheetName, ICollection<string> errors,
        out int value)
    {
        if (int.TryParse(row.Get(field), out value))
            return true;
        errors.Add($"{sheetName}!{field}{row.RowNumber}: must be an integer.");
        return false;
    }

    private static bool TryParseBoolean(TableRow row, string field, string sheetName, ICollection<string> errors,
        out bool value)
    {
        var raw = row.Get(field);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = false;
            return true;
        }
        if (bool.TryParse(raw, out value))
            return true;
        if (raw == "1")
        {
            value = true;
            return true;
        }
        if (raw == "0")
        {
            value = false;
            return true;
        }
        errors.Add($"{sheetName}!{field}{row.RowNumber}: accepts only true or false.");
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
        errors.Add($"{sheetName}!BlockType{row.RowNumber}: accepts only SINGLE or GROUP.");
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
        errors.Add($"{sheetName}!Type{row.RowNumber}: accepts only TEXT or IMAGE.");
        type = default;
        return false;
    }

    private static string? Required(TableRow row, string field, string sheetName, ICollection<string> errors)
    {
        var value = row.Get(field);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        errors.Add($"{sheetName}!{field}{row.RowNumber}: is required.");
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
