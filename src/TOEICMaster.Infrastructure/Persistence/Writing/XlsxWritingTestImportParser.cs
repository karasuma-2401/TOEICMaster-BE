using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TOEICMaster.Application.Writing.Admin;

namespace TOEICMaster.Infrastructure.Persistence.Writing;

public sealed class XlsxWritingTestImportParser : IWritingTestImportParser
{
    private static readonly string[][] Headers =
    [
        ["TestCode", "Title", "Description"],
        ["SequenceNumber", "ImageAssetId", "ImageAlt", "KeywordOne", "KeywordTwo", "RubricNotes", "ModelAnswer"],
        ["SequenceNumber", "Sender", "Recipient", "Subject", "EmailBody", "Directions", "RubricNotes", "ModelAnswer"],
        ["SequenceNumber", "Prompt", "MinimumWordCount", "Outline", "RubricNotes", "ModelAnswer"]
    ];
    private static readonly string[] SheetNames = ["Manifest", "Part1", "Part2", "Part3"];

    public WritingTestImportPreviewDto Preview(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.Entries.Count > 1_000 || archive.Entries.Sum(entry => entry.Length) > 32L * 1024 * 1024)
                throw new InvalidWritingImportException("Nội dung giải nén của file Excel quá lớn.");
            var workbook = LoadXml(archive, "xl/workbook.xml");
            var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
            var shared = sharedEntry is null ? [] : ReadShared(sharedEntry);
            var links = relationships.Descendants().Where(item => item.Name.LocalName == "Relationship")
                .Where(item => item.Attribute("Id") is not null && item.Attribute("Target") is not null)
                .ToDictionary(item => item.Attribute("Id")!.Value, item => item.Attribute("Target")!.Value,
                    StringComparer.Ordinal);
            var sheets = new Dictionary<string, IReadOnlyList<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in workbook.Descendants().Where(item => item.Name.LocalName == "sheet"))
            {
                var name = sheet.Attribute("name")?.Value;
                var id = sheet.Attributes().FirstOrDefault(item => item.Name.LocalName == "id")?.Value;
                if (name is null || id is null || !SheetNames.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                if (!links.TryGetValue(id, out var target)) throw new InvalidWritingImportException("Thiếu worksheet trong file.");
                var path = target.TrimStart('/');
                if (!path.StartsWith("xl/", StringComparison.Ordinal)) path = "xl/" + path;
                if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
                    throw new InvalidWritingImportException("Đường dẫn worksheet không hợp lệ.");
                if (!sheets.TryAdd(name, ReadSheet(LoadXml(archive, path), shared)))
                    throw new InvalidWritingImportException("Tên worksheet bị trùng.");
            }
            if (SheetNames.Any(name => !sheets.ContainsKey(name)))
                throw new InvalidWritingImportException("File cần đủ 4 sheet: Manifest, Part1, Part2, Part3.");
            var errors = new List<string>();
            var manifests = sheets["Manifest"];
            if (manifests.Count != 1) return new WritingTestImportPreviewDto(null, ["Manifest cần đúng một dòng dữ liệu."]);
            string Get(IReadOnlyDictionary<string, string> row, string key) => row.GetValueOrDefault(key)?.Trim() ?? "";
            var code = Get(manifests[0], "TestCode");
            var title = Get(manifests[0], "Title");
            var description = Get(manifests[0], "Description");
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
                errors.Add("Manifest cần TestCode và Title.");
            var questions = new List<WritingTestQuestionInput>();
            for (var part = 1; part <= 3; part++)
            {
                var rows = sheets[$"Part{part}"];
                var expected = part == 1 ? 5 : part == 2 ? 2 : 1;
                if (rows.Count != expected) errors.Add($"Part {part} cần đúng {expected} câu.");
                if (rows.Count > expected) continue;
                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];
                    var globalSequence = part == 1 ? index + 1 : part == 2 ? index + 6 : index + 8;
                    if (!int.TryParse(Get(row, "SequenceNumber"), out var sequence) || sequence != globalSequence)
                        errors.Add($"Part {part}, dòng {index + 2}: SequenceNumber phải là {globalSequence}.");
                    var imageId = Guid.TryParse(Get(row, "ImageAssetId"), out var parsedImage) ? parsedImage : (Guid?)null;
                    var minimum = int.TryParse(Get(row, "MinimumWordCount"), out var parsedMinimum) ? parsedMinimum : (int?)null;
                    var input = new WritingExerciseInput(part, $"{code}-P{part}-{index + 1}", title, "MEDIUM", "DRAFT",
                        Get(row, "RubricNotes"), Get(row, "ModelAnswer"), imageId, Get(row, "ImageAlt"),
                        Get(row, "KeywordOne"), Get(row, "KeywordTwo"), Get(row, "Sender"), Get(row, "Recipient"),
                        Get(row, "Subject"), Get(row, "EmailBody"), Get(row, "Directions"), Get(row, "Prompt"), minimum,
                        Get(row, "Outline"));
                    var validation = new WritingExerciseInputValidator().Validate(input);
                    errors.AddRange(validation.Errors.Select(error => $"Part {part}, dòng {index + 2}: {error.ErrorMessage}"));
                    questions.Add(new WritingTestQuestionInput(null, part, index + 1, input));
                }
            }
            var test = new WritingTestInput(code, title, description, questions);
            var testValidation = new WritingTestInputValidator().Validate(test);
            errors.AddRange(testValidation.Errors.Select(error => error.ErrorMessage));
            return new WritingTestImportPreviewDto(test, errors.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (InvalidWritingImportException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or System.Xml.XmlException or ArgumentException)
        {
            throw new InvalidWritingImportException("File Excel không hợp lệ.");
        }
    }

    public byte[] CreateTemplate()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet4.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Write(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Write(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Manifest" sheetId="1" r:id="rId1"/><sheet name="Part1" sheetId="2" r:id="rId2"/><sheet name="Part2" sheetId="3" r:id="rId3"/><sheet name="Part3" sheetId="4" r:id="rId4"/></sheets></workbook>
                """);
            Write(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/><Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet4.xml"/></Relationships>
                """);
            for (var sheet = 0; sheet < Headers.Length; sheet++)
            {
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var row = new XElement(ns + "row", new XAttribute("r", "1"), Headers[sheet].Select((header, index) =>
                    new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + index)}1"), new XAttribute("t", "inlineStr"),
                        new XElement(ns + "is", new XElement(ns + "t", header)))));
                var document = new XDocument(new XElement(ns + "worksheet", new XElement(ns + "sheetData", row)));
                Write(zip, $"xl/worksheets/sheet{sheet + 1}.xml", document.ToString(SaveOptions.DisableFormatting));
            }
        }
        return output.ToArray();
    }

    private static void Write(ZipArchive archive, string path, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidWritingImportException($"File thiếu {path}.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string[] ReadShared(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream).Descendants().Where(item => item.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants().Where(child => child.Name.LocalName == "t")
                .Select(child => child.Value))).ToArray();
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadSheet(XDocument document, string[] shared)
    {
        var rows = document.Descendants().Where(item => item.Name.LocalName == "row").Take(10_002).ToArray();
        if (rows.Length > 10_001) throw new InvalidWritingImportException("Worksheet có quá nhiều dòng.");
        if (rows.Length == 0) return [];
        Dictionary<int, string> Cells(XElement row) => row.Elements().Where(item => item.Name.LocalName == "c")
            .Select(item => (Index: Column(item.Attribute("r")?.Value), Value: Cell(item, shared)))
            .Where(item => item.Index > 0).GroupBy(item => item.Index)
            .ToDictionary(group => group.Key, group => group.Last().Value);
        var headers = Cells(rows[0]);
        if (headers.Values.Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            headers.Values.Count(value => value.Length > 0)) throw new InvalidWritingImportException("File có cột trùng tên.");
        return rows.Skip(1).Select(Cells).Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .Select(row => headers.Where(header => !string.IsNullOrWhiteSpace(header.Value)).ToDictionary(
                header => header.Value.Trim(), header => row.GetValueOrDefault(header.Key) ?? "", StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    private static int Column(string? reference)
    {
        var value = 0;
        foreach (var character in reference ?? "")
        {
            if (!char.IsAsciiLetter(character)) break;
            if (value > 16_384) return 0;
            value = value * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return value;
    }

    private static string Cell(XElement cell, string[] shared)
    {
        if (cell.Attribute("t")?.Value == "inlineStr")
            return string.Concat(cell.Descendants().Where(item => item.Name.LocalName == "t").Select(item => item.Value));
        var raw = cell.Elements().FirstOrDefault(item => item.Name.LocalName == "v")?.Value ?? "";
        return cell.Attribute("t")?.Value == "s" && int.TryParse(raw, out var index) && index >= 0 && index < shared.Length
            ? shared[index] : raw;
    }
}
