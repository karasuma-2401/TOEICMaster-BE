
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public sealed class SubmitLrPracticeCommandHandler(
    ILrExerciseBankRepository repository,
    ILrPracticeAttemptRepository attemptRepository
) : IRequestHandler<SubmitLrPracticeCommand, LrPracticeSubmitResultDto>
{
    public async Task<LrPracticeSubmitResultDto> Handle(
        SubmitLrPracticeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("Invalid user ID.");

        var item = await repository.GetPublishedByIdAsync(
            request.ItemId,
            cancellationToken);

        if (item is null)
            throw new KeyNotFoundException("Practice item not found.");

        if (request.Request.Answers is null)
            throw new ArgumentException("Answers are required.");

        var submittedAnswers = request.Request.Answers;

        // Không cho phép gửi trùng QuestionId.
        if (submittedAnswers
            .GroupBy(answer => answer.QuestionId)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Duplicate question answers are not allowed.");
        }

        var questionIds = item.Questions
            .Select(question => question.Id)
            .ToHashSet();

        // Chỉ chấp nhận câu hỏi thuộc bài tập đang làm.
        if (submittedAnswers.Any(answer =>
                !questionIds.Contains(answer.QuestionId)))
        {
            throw new ArgumentException("An answer contains an invalid question ID.");
        }

        var answerMap = submittedAnswers.ToDictionary(
            answer => answer.QuestionId,
            answer => answer.SelectedKey?.Trim().ToUpperInvariant());

        var results = item.Questions
            .OrderBy(question => question.Ordinal)
            .Select(question =>
            {
                answerMap.TryGetValue(question.Id, out var selectedKey);

                var correctKey = question.CorrectKey.Trim().ToUpperInvariant();

                var isCorrect = selectedKey == correctKey;

                return new LrPracticeQuestionResultDto(
                    question.Id,
                    question.Ordinal,
                    selectedKey,
                    correctKey,
                    isCorrect,
                    question.Translation,
                    question.Explanation
                );
            })
            .ToList();

        var correctCount = results.Count(result => result.IsCorrect);

        // Tạo dữ liệu lịch sử làm bài.
        var attempt = LrPracticeAttempt.Create(
            request.UserId,
            item.Id,
            results.Select(result => new LrPracticeAnswerSnapshot(
                result.QuestionId,
                result.Ordinal,
                result.SelectedKey,
                result.CorrectKey,
                result.IsCorrect,
                result.Translation,
                result.Explanation
            )).ToList(),
            DateTimeOffset.UtcNow
        );

        // Lưu lần làm bài và câu trả lời.
        await attemptRepository.AddAsync(attempt, cancellationToken);
        await attemptRepository.SaveChangesAsync(cancellationToken);

        // Trả kết quả về frontend như trước.
        return new LrPracticeSubmitResultDto(
            item.Id,
            results.Count,
            correctCount,
            results.Count - correctCount,
            results
        );
    }
}
