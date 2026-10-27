using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Exams.Commands.UpdateExamScoreProfile;

public sealed record UpdateExamScoreProfileCommand(Guid ExamId, Guid RevisionId, Guid ScoreProfileId) : IRequest;

public sealed class UpdateExamScoreProfileCommandValidator : AbstractValidator<UpdateExamScoreProfileCommand>
{
    public UpdateExamScoreProfileCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEqual(Guid.Empty);
        RuleFor(command => command.RevisionId).NotEqual(Guid.Empty);
        RuleFor(command => command.ScoreProfileId).NotEqual(Guid.Empty);
    }
}

public sealed class UpdateExamScoreProfileCommandHandler(IExamRepository repository)
    : IRequestHandler<UpdateExamScoreProfileCommand>
{
    public async Task Handle(UpdateExamScoreProfileCommand request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        if (await repository.FindScoreProfileAsync(request.ScoreProfileId, cancellationToken) is null)
            throw new ExamNotFoundException();
        revision.SetScoreProfile(request.ScoreProfileId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
