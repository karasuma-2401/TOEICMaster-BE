using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record GetWritingExercisesQuery(WritingExerciseFilter Filter) : IRequest<WritingExercisePageDto>;
public sealed record GetWritingExerciseQuery(Guid ExerciseId) : IRequest<WritingExerciseDto>;
public sealed record GetWritingTestsQuery(WritingTestFilter Filter) : IRequest<WritingTestPageDto>;
public sealed record GetWritingTestQuery(Guid TestId) : IRequest<WritingTestDto>;

public sealed class WritingExerciseFilterValidator : AbstractValidator<WritingExerciseFilter>
{
    public WritingExerciseFilterValidator()
    {
        RuleFor(filter => filter.Part).Must(part => part is null or >= 1 and <= 3);
        RuleFor(filter => filter.Query).Must(query => query is null || query.Trim().Length <= 100);
        RuleFor(filter => filter.Difficulty).Must(value => value is null || WritingExerciseInputValidator.IsDifficulty(value));
        RuleFor(filter => filter.Status).Must(value => value is null || WritingExerciseInputValidator.IsExerciseStatus(value));
        RuleFor(filter => filter.PageNumber).GreaterThan(0);
        RuleFor(filter => filter.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class WritingTestFilterValidator : AbstractValidator<WritingTestFilter>
{
    public WritingTestFilterValidator()
    {
        RuleFor(filter => filter.Query).Must(query => query is null || query.Trim().Length <= 100);
        RuleFor(filter => filter.Status).Must(value => value is null || value.Trim().ToUpperInvariant() is "DRAFT" or "PUBLISHED");
        RuleFor(filter => filter.PageNumber).GreaterThan(0);
        RuleFor(filter => filter.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetWritingExercisesQueryValidator : AbstractValidator<GetWritingExercisesQuery>
{
    public GetWritingExercisesQueryValidator() => RuleFor(query => query.Filter).SetValidator(new WritingExerciseFilterValidator());
}

public sealed class GetWritingTestsQueryValidator : AbstractValidator<GetWritingTestsQuery>
{
    public GetWritingTestsQueryValidator() => RuleFor(query => query.Filter).SetValidator(new WritingTestFilterValidator());
}

public sealed class GetWritingExercisesQueryHandler(IWritingAdminRepository repository)
    : IRequestHandler<GetWritingExercisesQuery, WritingExercisePageDto>
{
    public Task<WritingExercisePageDto> Handle(GetWritingExercisesQuery request, CancellationToken cancellationToken) =>
        repository.GetExercisePageAsync(request.Filter, cancellationToken);
}

public sealed class GetWritingExerciseQueryHandler(IWritingAdminRepository repository)
    : IRequestHandler<GetWritingExerciseQuery, WritingExerciseDto>
{
    public async Task<WritingExerciseDto> Handle(GetWritingExerciseQuery request, CancellationToken cancellationToken) =>
        await repository.GetExerciseAsync(request.ExerciseId, cancellationToken) ?? throw new Common.Exceptions.WritingResourceNotFoundException();
}

public sealed class GetWritingTestsQueryHandler(IWritingAdminRepository repository)
    : IRequestHandler<GetWritingTestsQuery, WritingTestPageDto>
{
    public Task<WritingTestPageDto> Handle(GetWritingTestsQuery request, CancellationToken cancellationToken) =>
        repository.GetTestPageAsync(request.Filter, cancellationToken);
}

public sealed class GetWritingTestQueryHandler(IWritingAdminRepository repository)
    : IRequestHandler<GetWritingTestQuery, WritingTestDto>
{
    public async Task<WritingTestDto> Handle(GetWritingTestQuery request, CancellationToken cancellationToken) =>
        await repository.GetTestAsync(request.TestId, cancellationToken) ?? throw new Common.Exceptions.WritingResourceNotFoundException();
}
