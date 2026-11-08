using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TOEICMaster.Infrastructure.Persistence.Writing;

namespace TOEICMaster.Tests.Infrastructure.Persistence.Writing;

public sealed class WritingImportParserTests
{
    [Fact]
    public void Test_workbook_can_be_imported_without_reference_answers()
    {
        var parser = new XlsxWritingTestImportParser();
        using var stream = new MemoryStream();
        stream.Write(parser.CreateTemplate());
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            AddRows(archive, 1, [["WT-001", "Writing test", "Description"]]);
            var image = Guid.NewGuid().ToString();
            AddRows(archive, 2, Enumerable.Range(1, 5).Select(index =>
                new[] { index.ToString(), image, "A photograph", "walk", "while", "", "" }).ToArray());
            AddRows(archive, 3, Enumerable.Range(6, 2).Select(index =>
                new[] { index.ToString(), "Sender", "Recipient", "Subject", "Email", "Respond", "", "" }).ToArray());
            AddRows(archive, 4, [["8", "Explain your opinion.", "300", "", "", ""]]);
        }

        var result = parser.Preview(stream.ToArray());

        Assert.True(result.IsValid, string.Join(", ", result.Errors));
        Assert.Equal(8, result.Test!.Questions.Count);
        Assert.All(result.Test.Questions, question => Assert.Null(question.ExerciseId));
        Assert.Equal(1, result.Test.Questions[5].SequenceNumber);
    }

    [Fact]
    public void Empty_test_template_cannot_be_imported()
    {
        var parser = new XlsxWritingTestImportParser();
        var result = parser.Preview(parser.CreateTemplate());
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    private static void AddRows(ZipArchive archive, int sheetNumber, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var path = $"xl/worksheets/sheet{sheetNumber}.xml";
        var entry = archive.GetEntry(path)!;
        XDocument document;
        using (var input = entry.Open()) document = XDocument.Load(input);
        entry.Delete();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var data = document.Descendants(ns + "sheetData").Single();
        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2;
            data.Add(new XElement(ns + "row", new XAttribute("r", rowNumber), rows[i].Select((value, column) =>
                new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + column)}{rowNumber}"),
                    new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value))))));
        }
        using var output = archive.CreateEntry(path).Open();
        document.Save(output);
    }

    [Fact]
    public void Preview_accepts_workbook_without_model_answer_column()
    {
        var bytes = CreateWorkbook(
            ["Code", "Topic", "Difficulty", "Status", "Prompt", "MinimumWordCount"],
            [["WP3-001", "Work", "HARD", "PUBLISHED", "State an opinion.", "300"]]);

        var preview = new XlsxWritingExerciseImportParser().Preview(3, "part-3.xlsx", bytes);

        Assert.True(Assert.Single(preview.Rows).IsValid);
    }

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
