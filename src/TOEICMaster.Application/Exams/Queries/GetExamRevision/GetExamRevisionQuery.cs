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
        var audioAssetIds = revision.Blocks
            .Where(block => block.AudioAssetId is not null)
            .Select(block => block.AudioAssetId!.Value)
            .Distinct()
            .ToArray();
        var mediaDurations = audioAssetIds.Length == 0
            ? null
            : await repository.GetMediaDurationsAsync(audioAssetIds, cancellationToken);
        return ExamRevisionDetailDto.From(exam, revision, mediaDurations);
    }
}
