using MediatR;

namespace TOEICMaster.Application.Exams.Commands.ForkExamRevision;

public sealed record ForkExamRevisionCommand(Guid ExamId, Guid SourceRevisionId) : IRequest<ExamDraftDto>;

public sealed class ForkExamRevisionCommandHandler(IExamRepository repository, TimeProvider timeProvider)
    : IRequestHandler<ForkExamRevisionCommand, ExamDraftDto>
{
    public async Task<ExamDraftDto> Handle(ForkExamRevisionCommand request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.CreateDraftRevision(request.SourceRevisionId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ExamDraftDto.From(exam, revision);
    }
}
