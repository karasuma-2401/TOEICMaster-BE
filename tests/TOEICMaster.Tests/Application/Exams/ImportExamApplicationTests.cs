using System.IO.Compression;
using System.Security;
using System.Text;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.ImportExam;
using TOEICMaster.Application.Exams.Importing;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class ImportExamApplicationTests
{
    [Fact]
    public void Builder_rejects_a_missing_fixed_question_slot()
    {
        var workbook = ValidWorkbookData();
        var invalid = workbook with { Questions = workbook.Questions.Where(question => question.QuestionNumber != 200).ToArray() };

        var exception = Assert.Throws<ExamWorkbookFormatException>(() => ImportedExamDraftBuilder.Build(invalid,
            "exam-audio/pending/test.wav", DateTimeOffset.UtcNow));

        Assert.Contains(exception.Errors, error => error.Contains("200", StringComparison.Ordinal));
    }

    [Fact]
    public void Reader_rejects_a_duplicate_required_header_as_a_validation_error()
    {
        var exception = Assert.Throws<ExamWorkbookFormatException>(() => XlsxExamWorkbookReader.Read(
            WorkbookBytes(ValidWorkbookData(), duplicateQuestionHeader: true)));

        Assert.Contains(exception.Errors, error => error.Contains("header 'BlockKey'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Valid_complete_workbook_and_master_audio_create_one_draft()
    {
        var profile = ScoreProfile.Create("Configured but not necessarily complete", DateTimeOffset.UtcNow);
        var source = ValidWorkbookData(profile.Id);
        var repository = new FakeExamRepository(profile);
        var store = new RecordingMediaStore();
        var handler = new ImportExamCommandHandler(repository, store, TimeProvider.System);
        var wav = "RIFF"u8.ToArray().Concat(new byte[4]).Concat("WAVEfmt "u8.ToArray()).ToArray();

        var result = await handler.Handle(new ImportExamCommand(WorkbookBytes(source), wav, "audio/wav"),
            CancellationToken.None);

        var exam = Assert.Single(repository.AddedExams);
        var revision = Assert.Single(exam.Revisions);
        Assert.Equal(200, result.QuestionCount);
        Assert.Equal(store.ObjectKey, revision.MasterAudioObjectKey);
        Assert.StartsWith($"exam-audio/{revision.Id:N}/", store.ObjectKey, StringComparison.Ordinal);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Invalid_workbook_does_not_upload_or_persist_a_draft()
    {
        var repository = new FakeExamRepository(ScoreProfile.Create("Profile", DateTimeOffset.UtcNow));
        var store = new RecordingMediaStore();
        var handler = new ImportExamCommandHandler(repository, store, TimeProvider.System);
        var wav = "RIFF"u8.ToArray().Concat(new byte[4]).Concat("WAVEfmt "u8.ToArray()).ToArray();

        await Assert.ThrowsAsync<ExamWorkbookFormatException>(() => handler.Handle(
            new ImportExamCommand("not a workbook"u8.ToArray(), wav, "audio/wav"), CancellationToken.None));

        Assert.Empty(repository.AddedExams);
        Assert.Empty(store.UploadedObjectKeys);
    }

    private static ImportedExamWorkbook ValidWorkbookData(Guid? scoreProfileId = null)
    {
        var profileId = scoreProfileId ?? Guid.NewGuid();
        var imageAssetId = Guid.NewGuid();
        var skeleton = Exam.Create("LR-2026-IMPORT", "Imported L&R", profileId, DateTimeOffset.UtcNow);
        var revision = Assert.Single(skeleton.Revisions);
        var blocks = revision.Blocks.Select(block => new ImportedExamBlock(block.Key, block.Part, block.Type,
            block.FirstQuestionNumber, block.Questions.Count)).ToArray();
        var contexts = revision.Blocks.Where(block => block.IsGrouped).Select(block => new ImportedExamContext(block.Key,
            1, ExamContextType.Text, $"Shared context for {block.Key}", null, null)).ToArray();
        var questions = revision.Blocks.SelectMany(block => block.Questions.Select(question => new ImportedExamQuestion(
            block.Key, question.Number, new ExamQuestionContent($"LR-{question.Number:000}", $"Stem {question.Number}",
                "Option A", "Option B", "Option C", block.Part == 2 ? null : "Option D", "A", null, null,
                block.Part == 1 ? imageAssetId : null)))).ToArray();
        return new ImportedExamWorkbook("LR-2026-IMPORT", "Imported L&R", profileId, blocks, contexts, questions);
    }

    private static byte[] WorkbookBytes(ImportedExamWorkbook workbook, bool duplicateQuestionHeader = false)
    {
        var questionHeaders = new[] { "BlockKey", "QuestionNumber", "QuestionCode", "Stem", "OptionA", "OptionB",
            "OptionC", "OptionD", "CorrectKey", "Translation", "Explanation", "ImageAssetId" };
        if (duplicateQuestionHeader)
            questionHeaders = questionHeaders.Append("BlockKey").ToArray();
        var sheets = new (string Name, IReadOnlyList<string[]> Rows)[]
        {
            ("Manifest", [
                ["Field", "Value"], ["SchemaVersion", "1"], ["TestCode", workbook.TestCode], ["Title", workbook.Title],
                ["Scope", "LISTENING_READING"], ["ScoreProfileId", workbook.ScoreProfileId.ToString()]
            ]),
            ("Blocks", workbook.Blocks.Select(block => new[] { block.Key, block.Part.ToString(), block.Type == ExamBlockType.Single ? "SINGLE" : "GROUP", block.FirstQuestionNumber.ToString(), block.QuestionCount.ToString() }).Prepend(["BlockKey", "Part", "BlockType", "FirstQuestionNumber", "QuestionCount"]).ToArray()),
            ("ContextBlocks", workbook.Contexts.Select(context => new[] { context.BlockKey, context.Ordinal.ToString(), context.Type == ExamContextType.Text ? "TEXT" : "IMAGE", context.Text ?? string.Empty, context.ImageAssetId?.ToString() ?? string.Empty, context.AltText ?? string.Empty }).Prepend(["BlockKey", "Ordinal", "Type", "Text", "ImageAssetId", "AltText"]).ToArray()),
            ("Questions", workbook.Questions.Select(question => new[] { question.BlockKey, question.QuestionNumber.ToString(), question.Content.Code, question.Content.Stem, question.Content.OptionA, question.Content.OptionB, question.Content.OptionC, question.Content.OptionD ?? string.Empty, question.Content.CorrectKey, question.Content.Translation ?? string.Empty, question.Content.Explanation ?? string.Empty, question.Content.ImageAssetId?.ToString() ?? string.Empty }).Prepend(questionHeaders).ToArray())
        };
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml(sheets));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", RelationshipXml(sheets));
            for (var index = 0; index < sheets.Length; index++)
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", WorksheetXml(sheets[index].Rows));
        }
        return stream.ToArray();
    }

    private static string WorkbookXml(IReadOnlyList<(string Name, IReadOnlyList<string[]> Rows)> sheets) =>
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
        string.Concat(sheets.Select((sheet, index) => "<sheet name=\"" + SecurityElement.Escape(sheet.Name) +
            "\" sheetId=\"" + (index + 1) + "\" r:id=\"rId" + (index + 1) + "\" />")) + "</sheets></workbook>";

    private static string RelationshipXml(IReadOnlyList<(string Name, IReadOnlyList<string[]> Rows)> sheets) =>
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        string.Concat(sheets.Select((_, index) => "<Relationship Id=\"rId" + (index + 1) + "\" Target=\"worksheets/sheet" +
            (index + 1) + ".xml\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" />")) +
        "</Relationships>";

    private static string WorksheetXml(IReadOnlyList<string[]> rows) =>
        "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
        string.Concat(rows.Select((row, rowIndex) => "<row r=\"" + (rowIndex + 1) + "\">" +
            string.Concat(row.Select((value, columnIndex) => "<c r=\"" + ColumnName(columnIndex) + (rowIndex + 1) +
                "\" t=\"inlineStr\"><is><t>" + SecurityElement.Escape(value) + "</t></is></c>")) + "</row>")) +
        "</sheetData></worksheet>";

    private static string ColumnName(int index)
    {
        var value = index + 1;
        var name = string.Empty;
        while (value > 0)
        {
            value--;
            name = (char)('A' + value % 26) + name;
            value /= 26;
        }
        return name;
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private sealed class RecordingMediaStore : IExamMediaStore
    {
        public List<string> UploadedObjectKeys { get; } = [];
        public string ObjectKey => Assert.Single(UploadedObjectKeys);
        public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
        {
            UploadedObjectKeys.Add(objectKey);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeExamRepository(ScoreProfile profile) : IExamRepository
    {
        public List<Exam> AddedExams { get; } = [];
        public bool Saved { get; private set; }
        public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) => Task.FromResult<Exam?>(null);
        public Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ExamPageDto([], pageNumber, pageSize, 0));
        public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
            Task.FromResult<ScoreProfile?>(profile.Id == scoreProfileId ? profile : null);
        public Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddExamAsync(Exam exam, CancellationToken cancellationToken)
        {
            AddedExams.Add(exam);
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
