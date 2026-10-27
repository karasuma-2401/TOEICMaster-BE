using MediatR;

namespace TOEICMaster.Application.Exams.Commands.PublishExamRevision;

public sealed record PublishExamRevisionCommand(Guid ExamId, Guid RevisionId) : IRequest<ExamDraftDto>;

public sealed class PublishExamRevisionCommandHandler(IExamRepository repository)
    : IRequestHandler<PublishExamRevisionCommand, ExamDraftDto>
{
    public async Task<ExamDraftDto> Handle(PublishExamRevisionCommand request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        var profile = await repository.FindScoreProfileAsync(revision.ScoreProfileId, cancellationToken)
            ?? throw new ExamNotFoundException();
        var errors = revision.GetPublicationErrors(profile.IsComplete);
        if (errors.Count > 0)
            throw new ExamPublicationException(errors);

        exam.PublishDraft(revision.Id);
        await repository.SaveChangesAsync(cancellationToken);
        return ExamDraftDto.From(exam, revision);
    }
}
