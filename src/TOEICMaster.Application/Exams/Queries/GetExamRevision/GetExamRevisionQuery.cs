using MediatR;

namespace TOEICMaster.Application.Exams.Queries.GetExamRevision;

public sealed record GetExamRevisionQuery(Guid ExamId, Guid RevisionId) : IRequest<ExamRevisionDetailDto>;

public sealed class GetExamRevisionQueryHandler(IExamRepository repository)
    : IRequestHandler<GetExamRevisionQuery, ExamRevisionDetailDto>
{
    public async Task<ExamRevisionDetailDto> Handle(GetExamRevisionQuery request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        return ExamRevisionDetailDto.From(exam, revision);
    }
}
