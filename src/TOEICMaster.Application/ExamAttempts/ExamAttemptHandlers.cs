using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

// 1. START ATTEMPT
public sealed record StartExamAttemptCommand(Guid UserId, Guid ExamId, int TimeLimitMinutes = 120) : IRequest<ExamAttemptSheetDto>;

public sealed class StartExamAttemptHandler(IExamAttemptRepository repository, ISender sender) : IRequestHandler<StartExamAttemptCommand, ExamAttemptSheetDto>
{
    public async Task<ExamAttemptSheetDto> Handle(StartExamAttemptCommand request, CancellationToken cancellationToken)
    {
        var activeAttemptId = await repository.FindActiveAttemptIdAsync(request.UserId, request.ExamId, cancellationToken);
        if (activeAttemptId.HasValue)
        {
            return await sender.Send(new GetExamAttemptProgressQuery(request.UserId, activeAttemptId.Value), cancellationToken);
        }

        var revision = await repository.FindPublishedRevisionForExamAsync(request.ExamId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(request.ExamId);

        var timeLimitSeconds = Math.Max(60, request.TimeLimitMinutes * 60);

        var attempt = new ExamAttempt(
            Guid.NewGuid(),
            request.UserId,
            revision.Id,
            revision.ScoreProfileId,
            DateTimeOffset.UtcNow,
            timeLimitSeconds
        );

        repository.AddAttempt(attempt);
        await repository.SaveChangesAsync(cancellationToken);

        return new ExamAttemptSheetDto(
            attempt.Id,
            request.ExamId,
            revision.VersionNumber,
            revision.MasterAudioObjectKey,
            attempt.StartedAtUtc,
            attempt.TimeLimitSeconds,
            attempt.GetRemainingSeconds(DateTimeOffset.UtcNow),
            revision.Blocks.OrderBy(b => (int)b.Part).ThenBy(b => b.FirstQuestionNumber).Select(b => new ExamBlockSheetDto(
                b.Id,
                (int)b.Part,
                b.FirstQuestionNumber,
                null,
                null,
                b.ContextBlocks.OrderBy(c => c.Ordinal).Select(c => new ExamContextBlockDto(c.Id, c.Ordinal, c.Text, c.ImageAssetId)).ToList(),
                b.Questions.OrderBy(q => q.Number).Select(q => new ExamQuestionSheetDto(
                    q.Id,
                    q.Number,
                    q.Stem,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.ImageAssetId,
                    null,
                    false
                )).ToList()
            )).ToList()
        );
    }
}

// 2. RESUME ATTEMPT
public sealed record GetExamAttemptProgressQuery(Guid UserId, Guid AttemptId) : IRequest<ExamAttemptSheetDto>;

public sealed class GetExamAttemptProgressHandler(IExamAttemptRepository repository) : IRequestHandler<GetExamAttemptProgressQuery, ExamAttemptSheetDto>
{
    public async Task<ExamAttemptSheetDto> Handle(GetExamAttemptProgressQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.GetAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Bài thi này đã kết thúc hoặc bị hủy, không thể tiếp tục làm.");

        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);

        var savedAnswers = attempt.Answers.ToDictionary(a => a.QuestionId);
        var now = DateTimeOffset.UtcNow;

        return new ExamAttemptSheetDto(
            attempt.Id,
            revision.ExamId,
            revision.VersionNumber,
            revision.MasterAudioObjectKey,
            attempt.StartedAtUtc,
            attempt.TimeLimitSeconds,
            attempt.GetRemainingSeconds(now),
            revision.Blocks.OrderBy(b => (int)b.Part).ThenBy(b => b.FirstQuestionNumber).Select(b => new ExamBlockSheetDto(
                b.Id,
                (int)b.Part,
                b.FirstQuestionNumber,
                null,
                null,
                b.ContextBlocks.OrderBy(c => c.Ordinal).Select(c => new ExamContextBlockDto(c.Id, c.Ordinal, c.Text, c.ImageAssetId)).ToList(),
                b.Questions.OrderBy(q => q.Number).Select(q => {
                    savedAnswers.TryGetValue(q.Id, out var userAns);
                    return new ExamQuestionSheetDto(
                        q.Id,
                        q.Number,
                        q.Stem,
                        q.OptionA,
                        q.OptionB,
                        q.OptionC,
                        q.OptionD,
                        q.ImageAssetId,
                        userAns?.SelectedKey,
                        userAns?.IsFlagged ?? false
                    );
                }).ToList()
            )).ToList()
        );
    }
}

// 3. GET ACTIVE ATTEMPT
public sealed record GetCurrentActiveAttemptQuery(Guid UserId, Guid ExamId) : IRequest<ExamAttemptSheetDto?>;

public sealed class GetCurrentActiveAttemptHandler(IExamAttemptRepository repository, ISender sender) : IRequestHandler<GetCurrentActiveAttemptQuery, ExamAttemptSheetDto?>
{
    public async Task<ExamAttemptSheetDto?> Handle(GetCurrentActiveAttemptQuery request, CancellationToken cancellationToken)
    {
        var activeAttemptId = await repository.FindActiveAttemptIdAsync(request.UserId, request.ExamId, cancellationToken);
        if (!activeAttemptId.HasValue) return null;

        return await sender.Send(new GetExamAttemptProgressQuery(request.UserId, activeAttemptId.Value), cancellationToken);
    }
}

// 4. SAVE ANSWERS
public sealed record SaveExamAttemptAnswersCommand(Guid UserId, Guid AttemptId, IReadOnlyList<SaveAttemptAnswerInput> Answers) : IRequest;

public sealed class SaveExamAttemptAnswersHandler(IExamAttemptRepository repository) : IRequestHandler<SaveExamAttemptAnswersCommand>
{
    public async Task Handle(SaveExamAttemptAnswersCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Không thể lưu đáp án cho bài thi đã nộp.");

        var questionIds = request.Answers.Select(x => x.QuestionId).ToList();
        var metadata = await repository.GetQuestionMetadataAsync(questionIds, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var ans in request.Answers)
        {
            if (metadata.TryGetValue(ans.QuestionId, out var qMeta))
            {
                attempt.UpsertAnswer(ans.QuestionId, qMeta.Number, qMeta.Part, ans.SelectedKey, ans.IsFlagged, now);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

// 5. TOGGLE FLAG
public sealed record ToggleQuestionFlagCommand(Guid UserId, Guid AttemptId, Guid QuestionId, bool IsFlagged) : IRequest;

public sealed class ToggleQuestionFlagHandler(IExamAttemptRepository repository) : IRequestHandler<ToggleQuestionFlagCommand>
{
    public async Task Handle(ToggleQuestionFlagCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Không thể đánh dấu cờ cho bài thi đã nộp.");

        var metadata = await repository.GetQuestionMetadataAsync([request.QuestionId], cancellationToken);
        if (!metadata.TryGetValue(request.QuestionId, out var qMeta))
            throw new ExamAttemptValidationException($"Không tìm thấy câu hỏi với mã '{request.QuestionId}'.");

        var now = DateTimeOffset.UtcNow;
        attempt.ToggleFlag(request.QuestionId, qMeta.Number, qMeta.Part, request.IsFlagged, now);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

// 6. ABANDON ATTEMPT
public sealed record AbandonExamAttemptCommand(Guid UserId, Guid AttemptId) : IRequest;

public sealed class AbandonExamAttemptHandler(IExamAttemptRepository repository) : IRequestHandler<AbandonExamAttemptCommand>
{
    public async Task Handle(AbandonExamAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Chỉ có thể hủy bài thi đang làm dở.");

        attempt.Abandon(DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

// 7. SUBMIT ATTEMPT
public sealed record SubmitExamAttemptCommand(Guid UserId, Guid AttemptId) : IRequest<ExamAttemptResultDto>;

public sealed class SubmitExamAttemptHandler(IExamAttemptRepository repository) : IRequestHandler<SubmitExamAttemptCommand, ExamAttemptResultDto>
{
    public async Task<ExamAttemptResultDto> Handle(SubmitExamAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status == ExamAttemptStatus.Completed)
            throw new ExamAttemptConflictException("Bài thi này đã được nộp trước đó.");

        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);

        var scoreProfile = await repository.FindScoreProfileAsync(attempt.ScoreProfileId, cancellationToken)
            ?? throw new ExamAttemptValidationException("Không tìm thấy bảng barem điểm quy đổi cho bài thi này.");

        var allQuestions = revision.Blocks.SelectMany(b => b.Questions.Select(q => new { q.Id, q.Number, Part = (int)b.Part, q.CorrectKey })).ToList();
        var correctKeys = allQuestions.ToDictionary(x => x.Id, x => x.CorrectKey);

        var now = DateTimeOffset.UtcNow;
        foreach (var q in allQuestions)
        {
            if (!attempt.Answers.Any(a => a.QuestionId == q.Id))
            {
                attempt.UpsertAnswer(q.Id, q.Number, q.Part, null, false, now);
            }
        }

        attempt.GradeAndComplete(scoreProfile, correctKeys, now);
        await repository.SaveChangesAsync(cancellationToken);

        var partSummaries = attempt.Answers
            .GroupBy(a => a.Part)
            .ToDictionary(
                g => g.Key,
                g => new PartScoreSummaryDto(
                    g.Key,
                    g.Count(),
                    g.Count(x => x.IsCorrect == true),
                    g.Count(x => x.IsCorrect == false && !string.IsNullOrWhiteSpace(x.SelectedKey)),
                    g.Count(x => string.IsNullOrWhiteSpace(x.SelectedKey))
                )
            );

        return new ExamAttemptResultDto(
            attempt.Id,
            attempt.ListeningRawScore!.Value,
            attempt.ReadingRawScore!.Value,
            attempt.ListeningScaledScore!.Value,
            attempt.ReadingScaledScore!.Value,
            attempt.TotalScaledScore!.Value,
            attempt.Answers.Count(x => x.IsCorrect == true),
            attempt.Answers.Count(x => x.IsCorrect == false && !string.IsNullOrWhiteSpace(x.SelectedKey)),
            attempt.Answers.Count(x => string.IsNullOrWhiteSpace(x.SelectedKey)),
            attempt.IsTimeExpired,
            attempt.StartedAtUtc,
            attempt.SubmittedAtUtc!.Value,
            partSummaries
        );
    }
}

// 8. REVIEW ATTEMPT
public sealed record GetExamAttemptReviewQuery(Guid UserId, Guid AttemptId, bool OnlyIncorrect = false) : IRequest<ExamAttemptReviewDto>;

public sealed class GetExamAttemptReviewHandler(IExamAttemptRepository repository) : IRequestHandler<GetExamAttemptReviewQuery, ExamAttemptReviewDto>
{
    public async Task<ExamAttemptReviewDto> Handle(GetExamAttemptReviewQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.GetAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.Completed)
            throw new ExamAttemptConflictException("Chỉ có thể xem lại bài làm sau khi đã hoàn thành nộp bài.");

        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);

        var userAnswers = attempt.Answers.ToDictionary(a => a.QuestionId);

        var questionsReview = new List<ExamAttemptReviewQuestionDto>();
        foreach (var block in revision.Blocks)
        {
            foreach (var q in block.Questions)
            {
                userAnswers.TryGetValue(q.Id, out var ans);
                bool isCorrect = ans?.IsCorrect ?? false;

                if (request.OnlyIncorrect && isCorrect)
                    continue;

                questionsReview.Add(new ExamAttemptReviewQuestionDto(
                    q.Id,
                    q.Number,
                    (int)block.Part,
                    q.Stem,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    ans?.SelectedKey,
                    q.CorrectKey,
                    isCorrect,
                    ans?.IsFlagged ?? false,
                    q.Explanation,
                    q.Translation,
                    q.ImageAssetId
                ));
            }
        }

        return new ExamAttemptReviewDto(
            attempt.Id,
            attempt.TotalScaledScore ?? 0,
            attempt.ListeningScaledScore ?? 0,
            attempt.ReadingScaledScore ?? 0,
            attempt.IsTimeExpired,
            questionsReview.OrderBy(q => q.Number).ToList()
        );
    }
}

// 9. HISTORY
public sealed record GetUserExamAttemptHistoryQuery(
    Guid UserId,
    int PageIndex = 1,
    int PageSize = 10,
    Guid? ExamId = null
) : IRequest<ExamAttemptHistoryPagedDto>;

public sealed class GetUserExamAttemptHistoryHandler(IExamAttemptRepository repository) : IRequestHandler<GetUserExamAttemptHistoryQuery, ExamAttemptHistoryPagedDto>
{
    public async Task<ExamAttemptHistoryPagedDto> Handle(GetUserExamAttemptHistoryQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.GetAttemptHistoryAsync(
            request.UserId,
            request.PageIndex,
            request.PageSize,
            request.ExamId,
            cancellationToken);

        return new ExamAttemptHistoryPagedDto(items, totalCount, request.PageIndex, request.PageSize);
    }
}
