using System.Text.RegularExpressions;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Importing;

public static partial class ImportedExamDraftBuilder
{
    public static Exam Build(ImportedExamWorkbook workbook, string masterAudioObjectKey, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(masterAudioObjectKey);

        var errors = new List<string>();
        ValidateIdentity(workbook, errors);
        if (errors.Count > 0)
            throw new ExamWorkbookFormatException(errors);

        var exam = Exam.Create(workbook.TestCode, workbook.Title, workbook.ScoreProfileId, createdAtUtc);
        var revision = exam.Revisions.Single();
        var expectedBlocks = revision.Blocks.ToDictionary(block => block.Key, StringComparer.Ordinal);
        ValidateBlocks(workbook.Blocks, expectedBlocks, errors);
        ValidateQuestions(workbook.Questions, expectedBlocks, errors);
        ValidateContexts(workbook.Contexts, expectedBlocks, errors);
        if (errors.Count > 0)
            throw new ExamWorkbookFormatException(errors);

        try
        {
            foreach (var block in revision.Blocks.Where(block => block.IsGrouped))
            {
                var contexts = workbook.Contexts.Where(context => context.BlockKey == block.Key)
                    .OrderBy(context => context.Ordinal).Select(context => new ExamContextContent(context.Ordinal,
                        context.Type, context.Text, context.ImageAssetId, context.AltText)).ToArray();
                revision.ReplaceContextBlocks(block.Key, contexts);
            }

            foreach (var question in workbook.Questions)
                revision.UpdateQuestion(question.QuestionNumber, question.Content);
            revision.SetMasterAudioObjectKey(masterAudioObjectKey);
        }
        catch (ArgumentException exception)
        {
            throw new ExamWorkbookFormatException([$"Workbook không đúng cấu trúc đề L&R: {exception.Message}"]);
        }

        var publicationErrors = revision.GetPublicationErrors(scoreProfileIsComplete: true);
        if (publicationErrors.Count > 0)
            throw new ExamWorkbookFormatException(publicationErrors);
        return exam;
    }

    public static IReadOnlyCollection<Guid> GetReferencedImageAssetIds(ImportedExamWorkbook workbook) => workbook.Questions
        .Select(question => question.Content.ImageAssetId).Concat(workbook.Contexts.Select(context => context.ImageAssetId))
        .Where(assetId => assetId is not null && assetId != Guid.Empty).Select(assetId => assetId!.Value).Distinct()
        .ToArray();

    private static void ValidateIdentity(ImportedExamWorkbook workbook, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(workbook.TestCode) || workbook.TestCode.Length > 64 ||
            !TestCodePattern().IsMatch(workbook.TestCode))
            errors.Add("Manifest.TestCode phải dài tối đa 64 ký tự và chỉ gồm chữ, số, '_' hoặc '-'.");
        if (string.IsNullOrWhiteSpace(workbook.Title) || workbook.Title.Length > 200)
            errors.Add("Manifest.Title là bắt buộc và dài tối đa 200 ký tự.");
        if (workbook.ScoreProfileId == Guid.Empty)
            errors.Add("Manifest.ScoreProfileId phải là UUID hợp lệ.");
    }

    private static void ValidateBlocks(IReadOnlyList<ImportedExamBlock> importedBlocks,
        IReadOnlyDictionary<string, ExamBlock> expectedBlocks, ICollection<string> errors)
    {
        foreach (var duplicate in importedBlocks.GroupBy(block => block.Key, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
            errors.Add($"Blocks có BlockKey bị lặp: '{duplicate.Key}'.");

        foreach (var block in importedBlocks)
        {
            if (!expectedBlocks.TryGetValue(block.Key, out var expected))
            {
                errors.Add($"Blocks chứa BlockKey không hợp lệ: '{block.Key}'.");
                continue;
            }
            if (block.Part != expected.Part || block.Type != expected.Type ||
                block.FirstQuestionNumber != expected.FirstQuestionNumber ||
                block.QuestionCount != expected.Questions.Count)
                errors.Add($"Blocks '{block.Key}' không khớp cấu trúc ETS cố định.");
        }

        foreach (var expected in expectedBlocks.Keys.Where(key => importedBlocks.All(block => block.Key != key)))
            errors.Add($"Blocks thiếu BlockKey bắt buộc: '{expected}'.");
    }

    private static void ValidateQuestions(IReadOnlyList<ImportedExamQuestion> importedQuestions,
        IReadOnlyDictionary<string, ExamBlock> expectedBlocks, ICollection<string> errors)
    {
        var expectedQuestionKeys = expectedBlocks.Values.SelectMany(block => block.Questions
            .Select(question => (block.Key, question.Number))).ToHashSet();
        foreach (var duplicate in importedQuestions.GroupBy(question => (question.BlockKey, question.QuestionNumber))
                     .Where(group => group.Count() > 1))
            errors.Add($"Questions có câu bị lặp: '{duplicate.Key.BlockKey}' / {duplicate.Key.QuestionNumber}.");

        foreach (var question in importedQuestions)
            if (!expectedQuestionKeys.Contains((question.BlockKey, question.QuestionNumber)))
                errors.Add($"Questions chứa cặp BlockKey / QuestionNumber không hợp lệ: '{question.BlockKey}' / {question.QuestionNumber}.");

        foreach (var expected in expectedQuestionKeys.Where(key => importedQuestions.All(question =>
                     question.BlockKey != key.Key || question.QuestionNumber != key.Number)))
            errors.Add($"Questions thiếu câu {expected.Number} trong block '{expected.Key}'.");
    }

    private static void ValidateContexts(IReadOnlyList<ImportedExamContext> importedContexts,
        IReadOnlyDictionary<string, ExamBlock> expectedBlocks, ICollection<string> errors)
    {
        foreach (var duplicate in importedContexts.GroupBy(context => (context.BlockKey, context.Ordinal))
                     .Where(group => group.Count() > 1))
            errors.Add($"ContextBlocks có Ordinal bị lặp: '{duplicate.Key.BlockKey}' / {duplicate.Key.Ordinal}.");

        foreach (var context in importedContexts)
            if (!expectedBlocks.TryGetValue(context.BlockKey, out var block) || !block.IsGrouped)
                errors.Add($"ContextBlocks chỉ được tham chiếu block nhóm hợp lệ: '{context.BlockKey}'.");

        foreach (var groupedBlock in expectedBlocks.Values.Where(block => block.IsGrouped))
            if (importedContexts.All(context => context.BlockKey != groupedBlock.Key))
                errors.Add($"ContextBlocks thiếu ngữ cảnh cho block '{groupedBlock.Key}'.");
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TestCodePattern();
}
