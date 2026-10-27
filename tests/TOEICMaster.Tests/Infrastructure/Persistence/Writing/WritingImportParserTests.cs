using System.IO.Compression;
using System.Text;
using TOEICMaster.Infrastructure.Persistence.Writing;

namespace TOEICMaster.Tests.Infrastructure.Persistence.Writing;

public sealed class WritingImportParserTests
{
    [Fact]
    public void Part_three_preview_reports_valid_and_invalid_rows_without_persisting_anything()
    {
        var bytes = CreateWorkbook(
            ["Code", "Topic", "Difficulty", "Status", "Prompt", "MinimumWordCount", "ModelAnswer"],
            [
                ["WP3-001", "Work", "HARD", "PUBLISHED", "State an opinion.", "300", "A model essay."],
                ["", "Work", "EASY", "INVALID", "Prompt", "zero", "Model"]
            ]);

        var preview = new XlsxWritingExerciseImportParser().Preview(3, "part-3.xlsx", bytes);

        Assert.Equal(2, preview.Rows.Count);
        Assert.True(preview.Rows[0].IsValid);
        Assert.False(preview.Rows[1].IsValid);
        Assert.Contains(preview.Rows[1].Errors, error => error.Contains("Code", StringComparison.Ordinal));
        Assert.Contains(preview.Rows[1].Errors, error => error.Contains("Status", StringComparison.Ordinal));
        Assert.Equal(1, preview.ValidRowCount);
        Assert.Equal(1, preview.InvalidRowCount);
    }

    private static byte[] CreateWorkbook(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, 1_024, leaveOpen: false);
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            WriteRow(writer, 1, headers);
            for (var index = 0; index < rows.Count; index++)
                WriteRow(writer, index + 2, rows[index]);
            writer.Write("</sheetData></worksheet>");
        }
        return output.ToArray();
    }

    private static void WriteRow(TextWriter writer, int rowNumber, IReadOnlyList<string> values)
    {
        writer.Write($"<row r=\"{rowNumber}\">");
        for (var index = 0; index < values.Count; index++)
        {
            var column = ((char)('A' + index)).ToString();
            var escaped = System.Security.SecurityElement.Escape(values[index]) ?? string.Empty;
            writer.Write($"<c r=\"{column}{rowNumber}\" t=\"inlineStr\"><is><t>{escaped}</t></is></c>");
        }
        writer.Write("</row>");
    }
}
