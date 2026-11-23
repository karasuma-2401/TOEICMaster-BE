using System.Text.RegularExpressions;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Importing;

public static partial class ImportedExamDraftBuilder
{
    public static Exam Build(ImportedExamWorkbook workbook, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(workbook);

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
                        context.Type, context.Text, context.ImageAssetId, context.AltText, context.ImageRequired)).ToArray();
                revision.ReplaceContextBlocks(block.Key, contexts);
            }

            foreach (var question in workbook.Questions)
                revision.UpdateQuestion(question.QuestionNumber, question.Content);
        }
        catch (ArgumentException exception)
        {
            throw new ExamWorkbookFormatException([$"Workbook does not match the L&R exam structure: {exception.Message}"]);
        }

        var publicationErrors = revision.GetPublicationErrors(scoreProfileIsComplete: true)
            .Where(error => !error.StartsWith("Audio group ", StringComparison.Ordinal) &&
                !error.Contains("requires an image asset", StringComparison.Ordinal))
            .ToArray();
        if (publicationErrors.Length > 0)
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
            errors.Add("Manifest.TestCode must be at most 64 characters long and contain only letters, digits, '_' or '-'.");
        if (string.IsNullOrWhiteSpace(workbook.Title) || workbook.Title.Length > 200)
            errors.Add("Manifest.Title is required and must be at most 200 characters long.");
        if (workbook.ScoreProfileId == Guid.Empty)
            errors.Add("Manifest.ScoreProfileId must be a valid UUID.");
    }

    private static void ValidateBlocks(IReadOnlyList<ImportedExamBlock> importedBlocks,
        IReadOnlyDictionary<string, ExamBlock> expectedBlocks, ICollection<string> errors)
    {
        foreach (var duplicate in importedBlocks.GroupBy(block => block.Key, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
            errors.Add($"Blocks has duplicate BlockKey: '{duplicate.Key}'.");

        foreach (var block in importedBlocks)
        {
            if (!expectedBlocks.TryGetValue(block.Key, out var expected))
            {
                errors.Add($"Blocks contains invalid BlockKey: '{block.Key}'.");
                continue;
            }
            if (block.Part != expected.Part || block.Type != expected.Type ||
                block.FirstQuestionNumber != expected.FirstQuestionNumber ||
                block.QuestionCount != expected.Questions.Count)
                errors.Add($"Blocks '{block.Key}' does not match the fixed ETS structure.");
        }

        foreach (var expected in expectedBlocks.Keys.Where(key => importedBlocks.All(block => block.Key != key)))
            errors.Add($"Blocks is missing required BlockKey: '{expected}'.");
    }

    private static void ValidateQuestions(IReadOnlyList<ImportedExamQuestion> importedQuestions,
        IReadOnlyDictionary<string, ExamBlock> expectedBlocks, ICollection<string> errors)
    {
        var expectedQuestionKeys = expectedBlocks.Values.SelectMany(block => block.Questions
            .Select(question => (block.Key, question.Number))).ToHashSet();
        foreach (var duplicate in importedQuestions.GroupBy(question => (question.BlockKey, question.QuestionNumber))
                     .Where(group => group.Count() > 1))
            errors.Add($"Questions has duplicate entry: '{duplicate.Key.BlockKey}' / {duplicate.Key.QuestionNumber}.");

        foreach (var question in importedQuestions)
            if (!expectedQuestionKeys.Contains((question.BlockKey, question.QuestionNumber)))
                errors.Add($"Questions contains invalid BlockKey / QuestionNumber pair: '{question.BlockKey}' / {question.QuestionNumber}.");

        foreach (var expected in expectedQuestionKeys.Where(key => importedQuestions.All(question =>
                     question.BlockKey != key.Key || question.QuestionNumber != key.Number)))
            errors.Add($"Questions is missing question {expected.Number} in block '{expected.Key}'.");
    }

    private static void ValidateContexts(IReadOnlyList<ImportedExamContext> importedContexts,
        IReadOnlyDictionary<string, ExamBlock> expectedBlocks, ICollection<string> errors)
    {
        foreach (var duplicate in importedContexts.GroupBy(context => (context.BlockKey, context.Ordinal))
                     .Where(group => group.Count() > 1))
            errors.Add($"ContextBlocks has duplicate Ordinal: '{duplicate.Key.BlockKey}' / {duplicate.Key.Ordinal}.");

        foreach (var context in importedContexts)
            if (!expectedBlocks.TryGetValue(context.BlockKey, out var block) || !block.IsGrouped)
                errors.Add($"ContextBlocks may only reference a valid grouped block: '{context.BlockKey}'.");

        foreach (var groupedBlock in expectedBlocks.Values.Where(block => block.IsGrouped))
            if (importedContexts.All(context => context.BlockKey != groupedBlock.Key))
                errors.Add($"ContextBlocks is missing context for block '{groupedBlock.Key}'.");
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TestCodePattern();
}
