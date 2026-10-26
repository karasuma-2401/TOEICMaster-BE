using MediatR;

namespace TOEICMaster.Application.Exams.Queries.GetScoreProfiles;

public sealed record GetScoreProfilesQuery : IRequest<IReadOnlyList<ScoreProfileDto>>;

public sealed class GetScoreProfilesQueryHandler(IExamRepository repository)
    : IRequestHandler<GetScoreProfilesQuery, IReadOnlyList<ScoreProfileDto>>
{
    public async Task<IReadOnlyList<ScoreProfileDto>> Handle(GetScoreProfilesQuery request,
        CancellationToken cancellationToken) => await repository.GetScoreProfilesAsync(cancellationToken);
}
