using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.ExerciseBank;

public sealed record GetLrExerciseBankSummaryQuery : IRequest<LrExerciseBankSummaryDto>;
public sealed record GetLrExerciseItemPageQuery(LrExerciseItemFilter Filter) : IRequest<LrExerciseItemPageDto>;

public sealed class LrExerciseItemFilterValidator : AbstractValidator<LrExerciseItemFilter>
{
    public LrExerciseItemFilterValidator()
    {
        RuleFor(filter => filter.Part).InclusiveBetween(1, 7);
        RuleFor(filter => filter.Query).MaximumLength(100);
        RuleFor(filter => filter.Difficulty).Must(value => value is null ||
            new[] { "EASY", "MEDIUM", "HARD" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase));
        RuleFor(filter => filter.Status).Must(value => value is null ||
            new[] { "DRAFT", "PUBLISHED", "ARCHIVED" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase));
        RuleFor(filter => filter.PageNumber).GreaterThan(0);
        RuleFor(filter => filter.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetLrExerciseItemPageQueryValidator : AbstractValidator<GetLrExerciseItemPageQuery>
{
    public GetLrExerciseItemPageQueryValidator() => RuleFor(query => query.Filter).SetValidator(new LrExerciseItemFilterValidator());
}

public sealed class GetLrExerciseBankSummaryQueryHandler(ILrExerciseBankRepository repository)
    : IRequestHandler<GetLrExerciseBankSummaryQuery, LrExerciseBankSummaryDto>
{
    public Task<LrExerciseBankSummaryDto> Handle(GetLrExerciseBankSummaryQuery request,
        CancellationToken cancellationToken) => repository.GetSummaryAsync(cancellationToken);
}

public sealed class GetLrExerciseItemPageQueryHandler(ILrExerciseBankRepository repository)
    : IRequestHandler<GetLrExerciseItemPageQuery, LrExerciseItemPageDto>
{
    public Task<LrExerciseItemPageDto> Handle(GetLrExerciseItemPageQuery request,
        CancellationToken cancellationToken) => repository.GetPageAsync(request.Filter, cancellationToken);
}
