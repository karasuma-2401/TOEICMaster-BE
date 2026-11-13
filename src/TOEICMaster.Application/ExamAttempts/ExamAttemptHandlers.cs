using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExamAttempts;

/// <summary>Rules and loading steps shared by several attempt handlers.</summary>
internal static class ExamAttemptSupport
{
    /// <summary>Autosave requests may arrive a moment after the deadline because of network latency.</summary>
    public const int SaveGraceSeconds = 5;

    public const int MaxAnswersPerRequest = 200;

    public static async Task<ExamHeader> RequireHeaderAsync(
        IExamAttemptRepository repository,
        Guid examId,
        CancellationToken cancellationToken) =>
        await repository.GetExamHeaderAsync(examId, cancellationToken)
        ?? throw ExamAttemptNotFoundException.ForExam(examId);

    public static async Task<ExamAttemptSheetDto> LoadSheetAsync(
        IExamAttemptRepository repository,
        ExamAttempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);
        var header = await RequireHeaderAsync(repository, revision.ExamId, cancellationToken);
        return ExamAttemptMapper.ToSheet(attempt, revision, header, now);
    }

    /// <summary>
    /// Grades a tracked in-progress attempt. Unanswered questions are stored as skipped. When the deadline has
    /// passed the attempt is closed at the deadline and flagged as timed out.
    /// </summary>
    public static async Task GradeAsync(
        IExamAttemptRepository repository,
        ExamAttempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);

        var scoreProfile = await repository.FindScoreProfileAsync(attempt.ScoreProfileId, cancellationToken)
            ?? throw new ExamAttemptValidationException("Không tìm thấy bảng barem điểm quy đổi cho bài thi này.");
        if (!scoreProfile.IsComplete)
            throw new ExamAttemptValidationException("Barem điểm của bài thi này chưa đủ 101 mốc cho Listening và Reading.");

        var questions = revision.Blocks
            .SelectMany(block => block.Questions.Select(question => new
            {
                question.Id,
                question.Number,
                block.Part,
                question.CorrectKey
            }))
            .ToList();

        var timedOut = attempt.IsTimeUp(now);
        var submittedAt = timedOut ? attempt.DeadlineUtc : now;

        var answered = attempt.Answers.Select(answer => answer.QuestionId).ToHashSet();
        foreach (var question in questions.Where(question => !answered.Contains(question.Id)))
            attempt.RecordBlankAnswer(question.Id, question.Number, question.Part, submittedAt);

        attempt.GradeAndComplete(
            scoreProfile,
            questions.ToDictionary(question => question.Id, question => question.CorrectKey),
            submittedAt,
            timedOut);

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Validates the questions an answer request refers to: they must belong to this attempt's revision, the
    /// chosen key must be a real option, and Listening questions must still be editable.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ExamQuestionMetadata>> RequireEditableQuestionsAsync(
        IExamAttemptRepository repository,
        ExamAttempt attempt,
        IReadOnlyCollection<Guid> questionIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Không thể thay đổi đáp án của bài thi đã nộp hoặc đã hủy.");

        if (attempt.IsTimeUp(now, SaveGraceSeconds))
            throw new ExamAttemptConflictException("Đã hết thời gian làm bài, không thể lưu thêm đáp án.");

        var metadata = await repository.GetQuestionMetadataAsync(questionIds, cancellationToken);
        foreach (var questionId in questionIds)
        {
            if (!metadata.TryGetValue(questionId, out var question) || question.RevisionId != attempt.RevisionId)
                throw new ExamAttemptValidationException($"Câu hỏi '{questionId}' không thuộc bài thi này.");

            if (attempt.IsListeningLocked && ExamAttempt.IsListeningPart(question.Part))
                throw new ExamAttemptConflictException(
                    $"Câu {question.Number} thuộc phần Listening đã khóa, không thể thay đổi.");
        }

        return metadata;
    }

    /// <summary>Returns the normalized key (A-D), or null to clear the selection.</summary>
    public static string? NormalizeKey(string? selectedKey, ExamQuestionMetadata question)
    {
        if (string.IsNullOrWhiteSpace(selectedKey)) return null;

        var key = selectedKey.Trim().ToUpperInvariant();
        var valid = key is "A" or "B" or "C" || (key == "D" && question.HasOptionD);
        if (!valid)
            throw new ExamAttemptValidationException($"Đáp án '{selectedKey}' không hợp lệ cho câu {question.Number}.");
        return key;
    }
}

// 1. OVERVIEW (instructions screen)
public sealed record GetLrExamOverviewQuery(Guid UserId, Guid ExamId) : IRequest<LrExamOverviewDto>;

public sealed class GetLrExamOverviewHandler(IExamAttemptRepository repository)
    : IRequestHandler<GetLrExamOverviewQuery, LrExamOverviewDto>
{
    public async Task<LrExamOverviewDto> Handle(GetLrExamOverviewQuery request, CancellationToken cancellationToken)
    {
        var header = await ExamAttemptSupport.RequireHeaderAsync(repository, request.ExamId, cancellationToken);
        var revision = await repository.FindPublishedRevisionForExamAsync(request.ExamId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(request.ExamId);
        var activeAttemptId = await repository.FindActiveAttemptIdAsync(request.UserId, request.ExamId, cancellationToken);

        var total = ExamAttemptMapper.CountQuestions(revision);
        var listening = ExamAttemptMapper.CountListeningQuestions(revision);

        return new LrExamOverviewDto(
            header.Id,
            header.Code,
            header.Title,
            total,
            listening,
            total - listening,
            ExamAttempt.DefaultTimeLimitSeconds / 60,
            ExamAttemptMapper.MinScaledScore,
            ExamAttemptMapper.MaxScaledScore,
            activeAttemptId);
    }
}

// 2. START (or resume) AN ATTEMPT
public sealed record StartExamAttemptCommand(Guid UserId, Guid ExamId) : IRequest<ExamAttemptSheetDto>;

public sealed class StartExamAttemptHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<StartExamAttemptCommand, ExamAttemptSheetDto>
{
    public async Task<ExamAttemptSheetDto> Handle(StartExamAttemptCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var activeAttemptId = await repository.FindActiveAttemptIdAsync(request.UserId, request.ExamId, cancellationToken);
        if (activeAttemptId.HasValue)
        {
            var active = await repository.FindAttemptForUpdateAsync(activeAttemptId.Value, request.UserId, cancellationToken)
                ?? throw ExamAttemptNotFoundException.ForAttempt(activeAttemptId.Value);

            if (!active.IsTimeUp(now))
                return await ExamAttemptSupport.LoadSheetAsync(repository, active, now, cancellationToken);

            // The learner left and never came back: close the old attempt at its deadline, then start fresh.
            await ExamAttemptSupport.GradeAsync(repository, active, now, cancellationToken);
        }

        var header = await ExamAttemptSupport.RequireHeaderAsync(repository, request.ExamId, cancellationToken);
        var revision = await repository.FindPublishedRevisionForExamAsync(request.ExamId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(request.ExamId);

        var attempt = new ExamAttempt(
            Guid.NewGuid(),
            request.UserId,
            revision.Id,
            revision.ScoreProfileId,
            now);

        repository.AddAttempt(attempt);
        await repository.SaveChangesAsync(cancellationToken);

        return ExamAttemptMapper.ToSheet(attempt, revision, header, now);
    }
}

// 3. RESUME BY ATTEMPT ID
public sealed record GetExamAttemptProgressQuery(Guid UserId, Guid AttemptId) : IRequest<ExamAttemptSheetDto>;

public sealed class GetExamAttemptProgressHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetExamAttemptProgressQuery, ExamAttemptSheetDto>
{
    public async Task<ExamAttemptSheetDto> Handle(GetExamAttemptProgressQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Bài thi này đã kết thúc hoặc bị hủy, không thể tiếp tục làm.");

        var now = timeProvider.GetUtcNow();
        if (attempt.IsTimeUp(now))
        {
            await ExamAttemptSupport.GradeAsync(repository, attempt, now, cancellationToken);
            throw new ExamAttemptConflictException("Đã hết thời gian làm bài, hệ thống đã tự động nộp bài của bạn.");
        }

        return await ExamAttemptSupport.LoadSheetAsync(repository, attempt, now, cancellationToken);
    }
}

// 4. CURRENT ACTIVE ATTEMPT FOR AN EXAM
public sealed record GetCurrentActiveAttemptQuery(Guid UserId, Guid ExamId) : IRequest<ExamAttemptSheetDto?>;

public sealed class GetCurrentActiveAttemptHandler(IExamAttemptRepository repository, ISender sender)
    : IRequestHandler<GetCurrentActiveAttemptQuery, ExamAttemptSheetDto?>
{
    public async Task<ExamAttemptSheetDto?> Handle(GetCurrentActiveAttemptQuery request, CancellationToken cancellationToken)
    {
        var activeAttemptId = await repository.FindActiveAttemptIdAsync(request.UserId, request.ExamId, cancellationToken);
        if (!activeAttemptId.HasValue) return null;

        return await sender.Send(new GetExamAttemptProgressQuery(request.UserId, activeAttemptId.Value), cancellationToken);
    }
}

// 5. MOVE FROM LISTENING TO READING
public sealed record StartReadingCommand(Guid UserId, Guid AttemptId) : IRequest<ExamAttemptSheetDto>;

public sealed class StartReadingHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<StartReadingCommand, ExamAttemptSheetDto>
{
    public async Task<ExamAttemptSheetDto> Handle(StartReadingCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Không thể chuyển sang Reading với bài thi đã nộp hoặc đã hủy.");

        var now = timeProvider.GetUtcNow();
        if (attempt.IsTimeUp(now))
        {
            await ExamAttemptSupport.GradeAsync(repository, attempt, now, cancellationToken);
            throw new ExamAttemptConflictException("Đã hết thời gian làm bài, hệ thống đã tự động nộp bài của bạn.");
        }

        attempt.StartReading(now);
        await repository.SaveChangesAsync(cancellationToken);

        return await ExamAttemptSupport.LoadSheetAsync(repository, attempt, now, cancellationToken);
    }
}

// 6. SAVE ANSWERS (autosave)
public sealed record SaveExamAttemptAnswersCommand(Guid UserId, Guid AttemptId, IReadOnlyList<SaveAttemptAnswerInput> Answers)
    : IRequest;

public sealed class SaveExamAttemptAnswersHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SaveExamAttemptAnswersCommand>
{
    public async Task Handle(SaveExamAttemptAnswersCommand request, CancellationToken cancellationToken)
    {
        if (request.Answers.Count > ExamAttemptSupport.MaxAnswersPerRequest)
            throw new ExamAttemptValidationException(
                $"Mỗi lần lưu tối đa {ExamAttemptSupport.MaxAnswersPerRequest} câu.");

        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        var now = timeProvider.GetUtcNow();
        var metadata = await ExamAttemptSupport.RequireEditableQuestionsAsync(
            repository,
            attempt,
            request.Answers.Select(answer => answer.QuestionId).Distinct().ToList(),
            now,
            cancellationToken);

        foreach (var answer in request.Answers)
        {
            var question = metadata[answer.QuestionId];
            var key = ExamAttemptSupport.NormalizeKey(answer.SelectedKey, question);
            attempt.UpsertAnswer(answer.QuestionId, question.Number, question.Part, key, answer.IsFlagged, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

// 7. TOGGLE FLAG
public sealed record ToggleQuestionFlagCommand(Guid UserId, Guid AttemptId, Guid QuestionId, bool IsFlagged) : IRequest;

public sealed class ToggleQuestionFlagHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<ToggleQuestionFlagCommand>
{
    public async Task Handle(ToggleQuestionFlagCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        var now = timeProvider.GetUtcNow();
        var metadata = await ExamAttemptSupport.RequireEditableQuestionsAsync(
            repository, attempt, [request.QuestionId], now, cancellationToken);

        var question = metadata[request.QuestionId];
        attempt.ToggleFlag(request.QuestionId, question.Number, question.Part, request.IsFlagged, now);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

// 8. ABANDON
public sealed record AbandonExamAttemptCommand(Guid UserId, Guid AttemptId) : IRequest;

public sealed class AbandonExamAttemptHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<AbandonExamAttemptCommand>
{
    public async Task Handle(AbandonExamAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Chỉ có thể hủy bài thi đang làm dở.");

        attempt.Abandon(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }
}

// 9. SUBMIT
public sealed record SubmitExamAttemptCommand(Guid UserId, Guid AttemptId) : IRequest<ExamAttemptResultDto>;

public sealed class SubmitExamAttemptHandler(IExamAttemptRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SubmitExamAttemptCommand, ExamAttemptResultDto>
{
    public async Task<ExamAttemptResultDto> Handle(SubmitExamAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await repository.FindAttemptForUpdateAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status == ExamAttemptStatus.Completed)
            throw new ExamAttemptConflictException("Bài thi này đã được nộp trước đó.");
        if (attempt.Status != ExamAttemptStatus.InProgress)
            throw new ExamAttemptConflictException("Bài thi này đã bị hủy, không thể nộp.");

        await ExamAttemptSupport.GradeAsync(repository, attempt, timeProvider.GetUtcNow(), cancellationToken);

        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);
        var header = await ExamAttemptSupport.RequireHeaderAsync(repository, revision.ExamId, cancellationToken);
        return ExamAttemptMapper.ToResult(attempt, header);
    }
}

// 10. RESULT (score report screen)
public sealed record GetExamAttemptResultQuery(Guid UserId, Guid AttemptId) : IRequest<ExamAttemptResultDto>;

public sealed class GetExamAttemptResultHandler(IExamAttemptRepository repository)
    : IRequestHandler<GetExamAttemptResultQuery, ExamAttemptResultDto>
{
    public async Task<ExamAttemptResultDto> Handle(GetExamAttemptResultQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.GetAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.Completed)
            throw new ExamAttemptConflictException("Chỉ xem được kết quả sau khi đã nộp bài.");

        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);
        var header = await ExamAttemptSupport.RequireHeaderAsync(repository, revision.ExamId, cancellationToken);
        return ExamAttemptMapper.ToResult(attempt, header);
    }
}

// 11. REVIEW (answer review screen)
public sealed record GetExamAttemptReviewQuery(Guid UserId, Guid AttemptId, ExamReviewFilter Filter = ExamReviewFilter.All)
    : IRequest<ExamAttemptReviewDto>;

public sealed class GetExamAttemptReviewHandler(IExamAttemptRepository repository)
    : IRequestHandler<GetExamAttemptReviewQuery, ExamAttemptReviewDto>
{
    public async Task<ExamAttemptReviewDto> Handle(GetExamAttemptReviewQuery request, CancellationToken cancellationToken)
    {
        var attempt = await repository.GetAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForAttempt(request.AttemptId);

        if (attempt.Status != ExamAttemptStatus.Completed)
            throw new ExamAttemptConflictException("Chỉ có thể xem lại bài làm sau khi đã hoàn thành nộp bài.");

        var revision = await repository.FindRevisionAsync(attempt.RevisionId, cancellationToken)
            ?? throw ExamAttemptNotFoundException.ForExam(attempt.RevisionId);
        var header = await ExamAttemptSupport.RequireHeaderAsync(repository, revision.ExamId, cancellationToken);
        return ExamAttemptMapper.ToReview(attempt, revision, header, request.Filter);
    }
}

// 12. HISTORY
public sealed record GetUserExamAttemptHistoryQuery(
    Guid UserId,
    int PageIndex = 1,
    int PageSize = 10,
    Guid? ExamId = null
) : IRequest<ExamAttemptHistoryPagedDto>;

public sealed class GetUserExamAttemptHistoryHandler(IExamAttemptRepository repository)
    : IRequestHandler<GetUserExamAttemptHistoryQuery, ExamAttemptHistoryPagedDto>
{
    public async Task<ExamAttemptHistoryPagedDto> Handle(
        GetUserExamAttemptHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await repository.GetAttemptHistoryAsync(
            request.UserId,
            pageIndex,
            pageSize,
            request.ExamId,
            cancellationToken);

        return new ExamAttemptHistoryPagedDto(items, totalCount, pageIndex, pageSize);
    }
}
