using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace TOEICMaster.Application.Speaking.Admin;

/// <summary>One data row; <see cref="HasInCellImage"/> marks Excel's "Place in Cell" pictures, which are not readable.</summary>
public sealed record SpeakingImportRow(int RowNumber, IReadOnlyDictionary<string, string> Cells,
    IReadOnlyList<byte[]> Images, bool HasInCellImage)
{
    public string Cell(string column) => Cells.GetValueOrDefault(SpeakingWorkbookReader.ColumnKey(column), string.Empty);
}

public sealed record SpeakingImportWorkbook(IReadOnlyDictionary<string, string> Test,
    IReadOnlyList<SpeakingImportRow> Rows);

/// <summary>
/// Reads the Speaking test template without third-party packages: sheet "Test" holds Code, Title and Description as
/// field/value pairs; sheet "Exercises" has a header row and one row per exercise, with the Part 2 picture floating
/// over its row.
/// </summary>
public static class SpeakingWorkbookReader
{
    public const int MaxBytes = 16 * 1024 * 1024;
    private const int MaxEntries = 128;
    private const long MaxUnpackedBytes = 48 * 1024 * 1024;
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelations = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private static readonly XNamespace DrawingMain = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static string ColumnKey(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static SpeakingImportWorkbook Read(byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new SpeakingMediaTooLargeException("File Excel không được vượt quá 16 MiB.");
        if (content.Length < 4 || content[0] != 'P' || content[1] != 'K')
            throw new InvalidSpeakingMediaException("File import phải là Excel .xlsx hợp lệ.");
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxEntries)
                throw Corrupt();
            var package = new Package(archive);
            var strings = ReadSharedStrings(package);
            var sheets = ReadSheetPaths(package);
            var test = ReadTestSheet(package, Sheet(sheets, "Test"), strings);
            return new SpeakingImportWorkbook(test, ReadExerciseRows(package, Sheet(sheets, "Exercises"), strings));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or FormatException or
                                           OverflowException or ArgumentException or NullReferenceException or
                                           KeyNotFoundException)
        {
            throw Corrupt();
        }
    }

    private static string Sheet(Dictionary<string, string> sheets, string name) =>
        sheets.TryGetValue(name, out var path)
            ? path
            : throw new InvalidSpeakingMediaException($"File Excel thiếu sheet \"{name}\".");

    private static Dictionary<string, string> ReadSheetPaths(Package package)
    {
        var workbook = package.Xml("xl/workbook.xml") ?? throw Corrupt();
        var relations = package.Relations("xl/workbook.xml");
        return workbook.Root!.Element(Main + "sheets")!.Elements(Main + "sheet")
            .Where(sheet => sheet.Attribute(OfficeRelations + "id") is { } id && relations.ContainsKey(id.Value))
            .ToDictionary(sheet => (string)sheet.Attribute("name")!,
                sheet => relations[(string)sheet.Attribute(OfficeRelations + "id")!].Path,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ReadSharedStrings(Package package)
    {
        var document = package.Xml("xl/sharedStrings.xml");
        return document is null
            ? []
            : document.Root!.Elements(Main + "si").Select(item => string.Concat(item.Descendants(Main + "t")
                .Where(text => text.Parent?.Name != Main + "rPh").Select(text => text.Value))).ToArray();
    }

    private static Dictionary<string, string> ReadTestSheet(Package package, string path, string[] strings)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in ReadRows(package, path, strings).Values)
            if (row.TryGetValue(1, out var field) && row.TryGetValue(2, out var value))
                values[ColumnKey(field.Text)] = value.Text;
        return values;
    }

    private static List<SpeakingImportRow> ReadExerciseRows(Package package, string path, string[] strings)
    {
        var rows = ReadRows(package, path, strings);
        if (!rows.TryGetValue(1, out var header))
            throw new InvalidSpeakingMediaException("Sheet \"Exercises\" cần dòng tiêu đề ở dòng 1.");
        var columns = header.ToDictionary(cell => cell.Key, cell => ColumnKey(cell.Value.Text));
        var images = ReadImages(package, path);

        var result = new List<SpeakingImportRow>();
        foreach (var rowNumber in rows.Keys.Union(images.Keys).Where(number => number > 1).Order())
        {
            var cells = rows.GetValueOrDefault(rowNumber) ?? [];
            var pictures = images.GetValueOrDefault(rowNumber) ?? [];
            var inCell = cells.Values.Any(cell => cell.IsInCellImage);
            if (pictures.Count == 0 && !inCell && cells.Values.All(cell => string.IsNullOrWhiteSpace(cell.Text)))
                continue;
            var named = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (column, cell) in cells)
                if (columns.TryGetValue(column, out var name) && name.Length > 0)
                    named[name] = cell.Text;
            result.Add(new SpeakingImportRow(rowNumber, named, pictures, inCell));
        }
        return result;
    }

    private static SortedDictionary<int, Dictionary<int, CellValue>> ReadRows(Package package, string path,
        string[] strings)
    {
        var document = package.Xml(path) ?? throw Corrupt();
        var rows = new SortedDictionary<int, Dictionary<int, CellValue>>();
        var lastRow = 0;
        foreach (var row in document.Root!.Element(Main + "sheetData")!.Elements(Main + "row"))
        {
            lastRow = (int?)row.Attribute("r") ?? lastRow + 1;
            var cells = new Dictionary<int, CellValue>();
            var lastColumn = 0;
            foreach (var cell in row.Elements(Main + "c"))
            {
                lastColumn = (string?)cell.Attribute("r") is { } reference ? ColumnNumber(reference) : lastColumn + 1;
                cells[lastColumn] = new CellValue(ReadCell(cell, strings), cell.Attribute("vm") is not null);
            }
            rows[lastRow] = cells;
        }
        return rows;
    }

    private static string ReadCell(XElement cell, string[] strings)
    {
        var value = cell.Element(Main + "v")?.Value ?? string.Empty;
        return ((string?)cell.Attribute("t")) switch
        {
            "s" => strings[int.Parse(value, CultureInfo.InvariantCulture)],
            "inlineStr" => string.Concat(cell.Element(Main + "is")!.Descendants(Main + "t").Select(text => text.Value)),
            "e" => string.Empty,
            _ => value
        };
    }

    private static int ColumnNumber(string reference)
    {
        var number = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            number = number * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        return number;
    }

    /// <summary>Floating pictures by the 1-based row their top-left corner sits on.</summary>
    private static Dictionary<int, List<byte[]>> ReadImages(Package package, string sheetPath)
    {
        var images = new Dictionary<int, List<byte[]>>();
        foreach (var drawing in package.Relations(sheetPath).Values.Where(relation => relation.Type.EndsWith("/drawing",
                     StringComparison.Ordinal)))
        {
            var document = package.Xml(drawing.Path);
            if (document is null)
                continue;
            var targets = package.Relations(drawing.Path);
            foreach (var anchor in document.Descendants().Where(element =>
                         element.Name == Drawing + "twoCellAnchor" || element.Name == Drawing + "oneCellAnchor"))
            {
                var row = anchor.Element(Drawing + "from")?.Element(Drawing + "row")?.Value;
                var embed = (string?)anchor.Descendants(DrawingMain + "blip").FirstOrDefault()
                    ?.Attribute(OfficeRelations + "embed");
                if (row is null || embed is null || !targets.TryGetValue(embed, out var target) ||
                    package.Read(target.Path) is not { } bytes)
                    continue;
                var rowNumber = int.Parse(row, CultureInfo.InvariantCulture) + 1;
                if (!images.TryGetValue(rowNumber, out var list))
                    images[rowNumber] = list = [];
                list.Add(bytes);
            }
        }
        return images;
    }

    private static InvalidSpeakingMediaException Corrupt() => new("File Excel bị hỏng hoặc không đúng mẫu.");

    private sealed record CellValue(string Text, bool IsInCellImage);

    private sealed record Relation(string Path, string Type);

    /// <summary>Single entry point for package parts, so one budget bounds everything a crafted file can unpack.</summary>
    private sealed class Package(ZipArchive archive)
    {
        private long _remaining = MaxUnpackedBytes;

        public byte[]? Read(string path)
        {
            var entry = archive.GetEntry(path);
            if (entry is null)
                return null;
            using var source = entry.Open();
            using var target = new MemoryStream();
            var buffer = new byte[81_920];
            int count;
            while ((count = source.Read(buffer)) > 0)
            {
                _remaining -= count;
                if (_remaining < 0)
                    throw Corrupt();
                target.Write(buffer, 0, count);
            }
            return target.ToArray();
        }

        public XDocument? Xml(string path)
        {
            if (Read(path) is not { } bytes)
                return null;
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            return XDocument.Load(reader);
        }

        public Dictionary<string, Relation> Relations(string partPath)
        {
            var slash = partPath.LastIndexOf('/');
            var directory = slash < 0 ? string.Empty : partPath[..slash];
            var document = Xml($"{directory}/_rels/{partPath[(slash + 1)..]}.rels".TrimStart('/'));
            return document is null
                ? []
                : document.Root!.Elements(PackageRelations + "Relationship").ToDictionary(
                    relation => (string)relation.Attribute("Id")!,
                    relation => new Relation(Resolve(directory, (string)relation.Attribute("Target")!),
                        (string)relation.Attribute("Type")!));
        }

        private static string Resolve(string directory, string target)
        {
            if (target.StartsWith('/'))
                return target.TrimStart('/');
            var segments = directory.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var segment in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "..")
                {
                    if (segments.Count > 0)
                        segments.RemoveAt(segments.Count - 1);
                }
                else if (segment != ".")
                {
                    segments.Add(segment);
                }
            }
            return string.Join('/', segments);
        }
    }
}
