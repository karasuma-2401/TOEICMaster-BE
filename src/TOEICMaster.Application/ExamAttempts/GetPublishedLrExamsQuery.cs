using MediatR;

namespace TOEICMaster.Application.ExamAttempts;

public sealed record GetPublishedLrExamsQuery : IRequest<IReadOnlyList<PublishedLrExamDto>>;

public sealed class GetPublishedLrExamsHandler(IExamAttemptRepository repository)
    : IRequestHandler<GetPublishedLrExamsQuery, IReadOnlyList<PublishedLrExamDto>>
{
    public Task<IReadOnlyList<PublishedLrExamDto>> Handle(
        GetPublishedLrExamsQuery request,
        CancellationToken cancellationToken) =>
        repository.GetPublishedExamsAsync(cancellationToken);
}
