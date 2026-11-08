using System.IO.Compression;
using System.Security;
using System.Text;
using FluentValidation;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingTestImportTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private readonly InMemorySpeaking _store = new();
    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 11, 8, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task A_valid_file_creates_a_draft_test_with_seven_exercises_and_its_pictures()
    {
        var result = await ImportAsync(WorkbookBuilder.Valid());

        Assert.Equal(("IMP-01", "DRAFT", 7), (result.Code, result.Status, result.Exercises.Count));
        Assert.Equal(7, _store.Exercises.Count);
        Assert.All(_store.Exercises, exercise => Assert.Equal(SpeakingExerciseStatus.Draft, exercise.Status));
        Assert.Equal(2, _store.Objects.Count);
        var picture = result.Exercises.First(slot => slot.Part == 2).Exercise;
        Assert.Equal(_store.Assets[0].Id, picture.ImageAssetId);
        var document = result.Exercises.Single(slot => slot.Part == 4).Exercise;
        Assert.Equal([new SpeakingInfoRowDto("9:00", "Registration | desk"), new SpeakingInfoRowDto("9:30", "Keynote")],
            document.DocumentRows);
        Assert.Equal(["Q4 one?", "Q4 two?", "Q4 three?"], document.Questions);
    }

    [Fact]
    public async Task Row_errors_are_reported_together_and_nothing_is_saved()
    {
        var builder = WorkbookBuilder.Valid();
        builder.Rows[0]["Passage"] = "";
        builder.Rows[4]["Question3"] = "Q3 one?";
        builder.Rows[6]["Difficulty"] = "EXTREME";

        var error = await Assert.ThrowsAsync<ValidationException>(() => ImportAsync(builder));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[2].Passage");
        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[6].Questions");
        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[8].Difficulty");
        Assert.Empty(_store.Exercises);
        Assert.Empty(_store.Tests);
        Assert.Empty(_store.Objects);
    }

    [Fact]
    public async Task Missing_or_invalid_pictures_are_rejected()
    {
        var builder = WorkbookBuilder.Valid();
        builder.Pictures.Remove(4);
        builder.Pictures[5] = "GIF89a not a picture"u8.ToArray();

        var error = await Assert.ThrowsAsync<ValidationException>(() => ImportAsync(builder));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[4].ImageAssetId");
        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[5].Image");
    }

    [Fact]
    public async Task Text_or_pictures_repeated_inside_the_file_are_rejected()
    {
        var builder = WorkbookBuilder.Valid();
        builder.Rows[1]["Passage"] = "PASSAGE one!";
        builder.Rows[6]["Prompt"] = "q3 one";
        builder.Pictures[5] = builder.Pictures[4];

        var error = await Assert.ThrowsAsync<ValidationException>(() => ImportAsync(builder));

        Assert.Equal(3, error.Errors.Count(failure => failure.PropertyName == "Content"));
    }

    [Fact]
    public async Task Content_and_codes_already_in_the_bank_are_rejected()
    {
        var create = new CreateSpeakingExerciseCommandHandler(_store, _clock);
        await create.Handle(new CreateSpeakingExerciseCommand(Admin, SpeakingSamples.Exercise(5, "SP5-OLD") with
        {
            Prompt = "Prompt five"
        }), CancellationToken.None);
        await create.Handle(new CreateSpeakingExerciseCommand(Admin, SpeakingSamples.Exercise(1, "IMP-P1-1") with
        {
            Passage = "Something else entirely"
        }), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ImportAsync(WorkbookBuilder.Valid()));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[8].Prompt" && failure.ErrorMessage.Contains("SP5-OLD"));
        Assert.Contains(error.Errors, failure => failure.PropertyName == "Rows[2].Code");
    }

    [Fact]
    public async Task A_file_must_describe_the_whole_test()
    {
        var builder = WorkbookBuilder.Valid();
        builder.Rows.RemoveAt(6);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ImportAsync(builder));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "Exercises");
    }

    [Fact]
    public async Task Uploaded_pictures_are_removed_when_saving_fails()
    {
        _store.PendingConcurrencyConflicts = 1;

        await Assert.ThrowsAsync<SpeakingConcurrencyException>(() => ImportAsync(WorkbookBuilder.Valid()));

        Assert.Empty(_store.Objects);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
    [InlineData(new byte[] { (byte)'P', (byte)'K', 3, 4, 5, 6 })]
    public async Task Files_that_are_not_valid_workbooks_are_rejected(byte[] content)
    {
        await Assert.ThrowsAsync<InvalidSpeakingMediaException>(() => new ImportSpeakingTestCommandHandler(_store, _store,
            _clock).Handle(new ImportSpeakingTestCommand(Admin, content), CancellationToken.None));
    }

    [Fact]
    public void Oversized_files_and_missing_sheets_are_rejected()
    {
        Assert.Throws<SpeakingMediaTooLargeException>(() =>
            SpeakingWorkbookReader.Read(new byte[SpeakingWorkbookReader.MaxBytes + 1]));
        var error = Assert.Throws<InvalidSpeakingMediaException>(() =>
            SpeakingWorkbookReader.Read(WorkbookBuilder.Valid().Build(includeTestSheet: false)));
        Assert.Contains("Test", error.Message);
    }

    private Task<SpeakingTestDetailDto> ImportAsync(WorkbookBuilder builder) =>
        new ImportSpeakingTestCommandHandler(_store, _store, _clock).Handle(
            new ImportSpeakingTestCommand(Admin, builder.Build()), CancellationToken.None);

    /// <summary>Writes a minimal real .xlsx: inline strings, and pictures anchored through a drawing part.</summary>
    internal sealed class WorkbookBuilder
    {
        private static readonly string[] Columns =
        [
            "Part", "Sequence", "Code", "Topic", "Difficulty", "Status", "Model Answer", "Rubric Notes", "Passage",
            "Image Alt", "Context", "Question 1", "Question 2", "Question 3", "Document Title", "Document Rows",
            "Caller Intro", "Prompt"
        ];

        public List<Dictionary<string, string>> Rows { get; } = [];
        /// <summary>Picture bytes by 1-based sheet row.</summary>
        public Dictionary<int, byte[]> Pictures { get; } = [];

        public static WorkbookBuilder Valid()
        {
            var builder = new WorkbookBuilder();
            builder.Add(1, 1, new() { ["Passage"] = "Passage one." });
            builder.Add(1, 2, new() { ["Passage"] = "Passage two." });
            builder.Add(2, 1, new() { ["ImageAlt"] = "Picture one" });
            builder.Add(2, 2, new() { ["ImageAlt"] = "Picture two" });
            builder.Add(3, 1, new()
            {
                ["Context"] = "Survey context",
                ["Question1"] = "Q3 one?",
                ["Question2"] = "Q3 two?",
                ["Question3"] = "Q3 three?"
            });
            builder.Add(4, 1, new()
            {
                ["DocumentTitle"] = "Agenda",
                ["DocumentRows"] = "9:00 | Registration | desk\n9:30|Keynote",
                ["Question1"] = "Q4 one?",
                ["Question2"] = "Q4 two?",
                ["Question3"] = "Q4 three?"
            });
            builder.Add(5, 1, new() { ["Prompt"] = "Prompt five" });
            builder.Pictures[4] = SpeakingSamples.PngVariant(1);
            builder.Pictures[5] = SpeakingSamples.PngVariant(2);
            return builder;
        }

        private void Add(int part, int sequence, Dictionary<string, string> values)
        {
            values["Part"] = part.ToString();
            values["Sequence"] = sequence.ToString();
            values["Code"] = $"IMP-P{part}-{sequence}";
            values["Topic"] = "Chủ đề";
            values["Difficulty"] = "MEDIUM";
            values["ModelAnswer"] = "Model answer.";
            Rows.Add(values);
        }

        public byte[] Build(bool includeTestSheet = true)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                const string main = "xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
                const string rels = "xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"";
                var testSheet = includeTestSheet ? "<sheet name=\"Test\" sheetId=\"2\" r:id=\"rId2\"/>" : "";
                Text(archive, "xl/workbook.xml", $"<workbook {main}><sheets><sheet name=\"Exercises\" sheetId=\"1\" r:id=\"rId1\"/>{testSheet}</sheets></workbook>");
                Text(archive, "xl/_rels/workbook.xml.rels", $"<Relationships {rels}><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"worksheet\" Target=\"/xl/worksheets/sheet2.xml\"/></Relationships>");

                var sheet = new StringBuilder($"<worksheet {main}><sheetData>");
                sheet.Append(Row(1, Columns));
                for (var index = 0; index < Rows.Count; index++)
                    sheet.Append(Row(index + 2, Columns.Select(column =>
                        Rows[index].GetValueOrDefault(column.Replace(" ", string.Empty), string.Empty))));
                sheet.Append("</sheetData><drawing r:id=\"rId1\"/></worksheet>");
                Text(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
                Text(archive, "xl/worksheets/sheet2.xml", $"<worksheet {main}><sheetData>{Row(1, ["Code", "IMP-01"])}{Row(2, ["Title", "Đề import"])}{Row(3, ["Description", "Mô tả"])}</sheetData></worksheet>");
                if (Pictures.Count > 0)
                {
                    Text(archive, "xl/worksheets/_rels/sheet1.xml.rels", $"<Relationships {rels}><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/></Relationships>");
                    var drawing = new StringBuilder("<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
                    var relations = new StringBuilder($"<Relationships {rels}>");
                    foreach (var (row, bytes) in Pictures)
                    {
                        drawing.Append($"<xdr:twoCellAnchor><xdr:from><xdr:col>9</xdr:col><xdr:row>{row - 1}</xdr:row></xdr:from><xdr:pic><xdr:blipFill><a:blip r:embed=\"img{row}\"/></xdr:blipFill></xdr:pic></xdr:twoCellAnchor>");
                        relations.Append($"<Relationship Id=\"img{row}\" Type=\"image\" Target=\"../media/image{row}.png\"/>");
                        Bytes(archive, $"xl/media/image{row}.png", bytes);
                    }
                    Text(archive, "xl/drawings/drawing1.xml", drawing.Append("</xdr:wsDr>").ToString());
                    Text(archive, "xl/drawings/_rels/drawing1.xml.rels", relations.Append("</Relationships>").ToString());
                }
            }
            return stream.ToArray();
        }

        private static string Row(int number, IEnumerable<string> values)
        {
            var cells = values.Select((value, index) =>
                $"<c r=\"{(char)('A' + index)}{number}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(value)}</t></is></c>");
            return $"<row r=\"{number}\">{string.Concat(cells)}</row>";
        }

        private static void Text(ZipArchive archive, string path, string xml) => Bytes(archive, path, Encoding.UTF8.GetBytes(xml));

        private static void Bytes(ZipArchive archive, string path, byte[] content)
        {
            using var entry = archive.CreateEntry(path).Open();
            entry.Write(content);
        }
    }
}
