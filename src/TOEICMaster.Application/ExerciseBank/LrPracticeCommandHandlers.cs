using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public sealed class SubmitLrPracticeCommandHandler(
    ILrExerciseBankRepository repository,
    ILrPracticeAttemptRepository attemptRepository,
    TimeProvider timeProvider
) : IRequestHandler<SubmitLrPracticeCommand, LrPracticeSubmitResultDto>
{
    public async Task<LrPracticeSubmitResultDto> Handle(
        SubmitLrPracticeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new InvalidLrPracticeSubmissionException("Invalid user ID.");

        var item = await repository.GetPublishedByIdAsync(
            request.ItemId,
            cancellationToken);

        if (item is null)
            throw new LrPracticeItemNotFoundException("Practice item not found.");

        if (request.Request.Answers is null)
            throw new InvalidLrPracticeSubmissionException("Answers are required.");

        var submittedAnswers = request.Request.Answers;
        if (submittedAnswers
            .GroupBy(answer => answer.QuestionId)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidLrPracticeSubmissionException(
                "Duplicate question answers are not allowed.");
        }

        var questionsById = item.Questions.ToDictionary(question => question.Id);
        if (submittedAnswers.Any(answer => !questionsById.ContainsKey(answer.QuestionId)))
        {
            throw new InvalidLrPracticeSubmissionException(
                "An answer contains an invalid question ID.");
        }

        var answerMap = submittedAnswers.ToDictionary(
            answer => answer.QuestionId,
            answer => NormalizeSelectedKey(answer.SelectedKey, questionsById[answer.QuestionId]));

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
                    question.Explanation);
            })
            .ToList();

        var correctCount = results.Count(result => result.IsCorrect);
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
                result.Explanation)).ToList(),
            timeProvider.GetUtcNow());

        await attemptRepository.AddAsync(attempt, cancellationToken);
        await attemptRepository.SaveChangesAsync(cancellationToken);

        return new LrPracticeSubmitResultDto(
            item.Id,
            results.Count,
            correctCount,
            results.Count - correctCount,
            results);
    }

    private static string? NormalizeSelectedKey(
        string? selectedKey,
        LrExerciseBankQuestion question)
    {
        if (string.IsNullOrWhiteSpace(selectedKey))
            return null;

        var normalized = selectedKey.Trim().ToUpperInvariant();
        if (normalized.Length > 8 || normalized.Length != 1 || normalized is not ("A" or "B" or "C" or "D"))
        {
            throw new InvalidLrPracticeSubmissionException(
                "SelectedKey must be an available option key A, B, C, or D.");
        }

        var option = normalized switch
        {
            "A" => question.OptionA,
            "B" => question.OptionB,
            "C" => question.OptionC,
            "D" => question.OptionD,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(option))
        {
            throw new InvalidLrPracticeSubmissionException(
                "SelectedKey must reference an available option.");
        }

        return normalized;
    }
}
