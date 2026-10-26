using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Queries.GetExams;

public sealed record GetExamsQuery(ExamRevisionStatus? Status = null, int PageNumber = 1, int PageSize = 20)
    : IRequest<ExamPageDto>;

public sealed class GetExamsQueryValidator : AbstractValidator<GetExamsQuery>
{
    public GetExamsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetExamsQueryHandler(IExamRepository repository) : IRequestHandler<GetExamsQuery, ExamPageDto>
{
    public Task<ExamPageDto> Handle(GetExamsQuery request, CancellationToken cancellationToken) =>
        repository.GetExamPageAsync(request.Status, request.PageNumber, request.PageSize, cancellationToken);
}
