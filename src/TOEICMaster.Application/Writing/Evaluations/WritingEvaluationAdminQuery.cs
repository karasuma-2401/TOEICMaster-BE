using FluentValidation;
using MediatR;
using TOEICMaster.Application.Writing.Attempts;

namespace TOEICMaster.Application.Writing.Evaluations;

/// <summary>Protected read model for an administrator investigating a completed or failed AI evaluation.</summary>
public sealed record GetAdminWritingEvaluationQuery(Guid AttemptId) : IRequest<WritingAttemptDto>;

public sealed class GetAdminWritingEvaluationQueryValidator : AbstractValidator<GetAdminWritingEvaluationQuery>
{
    public GetAdminWritingEvaluationQueryValidator() => RuleFor(query => query.AttemptId).NotEmpty();
}

public sealed class GetAdminWritingEvaluationQueryHandler(IWritingAttemptRepository attemptRepository,
    IWritingEvaluationRepository evaluationRepository, TimeProvider timeProvider)
    : IRequestHandler<GetAdminWritingEvaluationQuery, WritingAttemptDto>
{
    public async Task<WritingAttemptDto> Handle(GetAdminWritingEvaluationQuery request,
        CancellationToken cancellationToken)
    {
        var attempt = await attemptRepository.GetAttemptAsync(request.AttemptId, cancellationToken)
            ?? throw new WritingAttemptNotFoundException();
        var evaluation = await evaluationRepository.GetByAttemptIdAsync(attempt.Id, cancellationToken)
            ?? throw new WritingAttemptConflictException("This Writing attempt does not have an AI evaluation result to review yet.");
        return attempt.ToDto(timeProvider.GetUtcNow(), evaluation);
    }
}
