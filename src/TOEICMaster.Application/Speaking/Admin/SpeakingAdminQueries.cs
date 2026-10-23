using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Speaking.Admin;

public sealed record GetSpeakingAdminSummaryQuery : IRequest<SpeakingAdminSummaryDto>;

public sealed record GetSpeakingExercisesQuery(SpeakingExerciseFilter Filter)
    : IRequest<SpeakingPageDto<SpeakingExerciseDto>>;

public sealed record GetSpeakingExerciseQuery(Guid ExerciseId) : IRequest<SpeakingExerciseDto>;

public sealed record GetSpeakingTestsQuery(SpeakingTestFilter Filter)
    : IRequest<SpeakingPageDto<SpeakingTestListItemDto>>;

public sealed record GetSpeakingTestQuery(Guid TestId) : IRequest<SpeakingTestDetailDto>;

public sealed class GetSpeakingExercisesQueryValidator : AbstractValidator<GetSpeakingExercisesQuery>
{
    public GetSpeakingExercisesQueryValidator()
    {
        RuleFor(query => query.Filter.Part).Must(part => part is null || Domain.Entities.SpeakingTiming.IsPart(part.Value))
            .OverridePropertyName("Part").WithMessage("Part phải từ 1 đến 5.");
        RuleFor(query => query.Filter.Query).Must(value => value is null || value.Trim().Length <= 100)
            .OverridePropertyName("Query").WithMessage("Từ khóa tối đa 100 ký tự.");
        RuleFor(query => query.Filter.Difficulty)
            .Must(value => string.IsNullOrWhiteSpace(value) || SpeakingCodes.TryParseDifficulty(value, out _))
            .OverridePropertyName("Difficulty").WithMessage("Độ khó không hợp lệ.");
        RuleFor(query => query.Filter.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) || SpeakingCodes.TryParseExerciseStatus(value, out _))
            .OverridePropertyName("Status").WithMessage("Trạng thái không hợp lệ.");
        RuleFor(query => query.Filter.PageNumber).GreaterThan(0).OverridePropertyName("PageNumber");
        RuleFor(query => query.Filter.PageSize).InclusiveBetween(1, 100).OverridePropertyName("PageSize");
    }
}

public sealed class GetSpeakingTestsQueryValidator : AbstractValidator<GetSpeakingTestsQuery>
{
    public GetSpeakingTestsQueryValidator()
    {
        RuleFor(query => query.Filter.Query).Must(value => value is null || value.Trim().Length <= 100)
            .OverridePropertyName("Query").WithMessage("Từ khóa tối đa 100 ký tự.");
        RuleFor(query => query.Filter.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) || SpeakingCodes.TryParseTestStatus(value, out _))
            .OverridePropertyName("Status").WithMessage("Trạng thái không hợp lệ.");
        RuleFor(query => query.Filter.PageNumber).GreaterThan(0).OverridePropertyName("PageNumber");
        RuleFor(query => query.Filter.PageSize).InclusiveBetween(1, 100).OverridePropertyName("PageSize");
    }
}

public sealed class GetSpeakingAdminSummaryQueryHandler(ISpeakingAdminRepository repository)
    : IRequestHandler<GetSpeakingAdminSummaryQuery, SpeakingAdminSummaryDto>
{
    public Task<SpeakingAdminSummaryDto> Handle(GetSpeakingAdminSummaryQuery request,
        CancellationToken cancellationToken) => repository.GetSummaryAsync(cancellationToken);
}

public sealed class GetSpeakingExercisesQueryHandler(ISpeakingAdminRepository repository)
    : IRequestHandler<GetSpeakingExercisesQuery, SpeakingPageDto<SpeakingExerciseDto>>
{
    public async Task<SpeakingPageDto<SpeakingExerciseDto>> Handle(GetSpeakingExercisesQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetExercisePageAsync(request.Filter, cancellationToken);
        return new SpeakingPageDto<SpeakingExerciseDto>(page.Items.Select(SpeakingAdminMapping.ToDto).ToArray(),
            page.PageNumber, page.PageSize, page.TotalCount);
    }
}

public sealed class GetSpeakingExerciseQueryHandler(ISpeakingAdminRepository repository)
    : IRequestHandler<GetSpeakingExerciseQuery, SpeakingExerciseDto>
{
    public async Task<SpeakingExerciseDto> Handle(GetSpeakingExerciseQuery request,
        CancellationToken cancellationToken) =>
        (await repository.FindExerciseAsync(request.ExerciseId, cancellationToken))?.ToDto()
        ?? throw new SpeakingNotFoundException("Không tìm thấy bài tập Speaking.");
}

public sealed class GetSpeakingTestsQueryHandler(ISpeakingAdminRepository repository)
    : IRequestHandler<GetSpeakingTestsQuery, SpeakingPageDto<SpeakingTestListItemDto>>
{
    public async Task<SpeakingPageDto<SpeakingTestListItemDto>> Handle(GetSpeakingTestsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetTestPageAsync(request.Filter, cancellationToken);
        return new SpeakingPageDto<SpeakingTestListItemDto>(
            page.Items.Select(SpeakingAdminMapping.ToListItemDto).ToArray(), page.PageNumber, page.PageSize,
            page.TotalCount);
    }
}

public sealed class GetSpeakingTestQueryHandler(ISpeakingAdminRepository repository)
    : IRequestHandler<GetSpeakingTestQuery, SpeakingTestDetailDto>
{
    public async Task<SpeakingTestDetailDto> Handle(GetSpeakingTestQuery request, CancellationToken cancellationToken)
    {
        var loaded = await repository.FindTestAsync(request.TestId, cancellationToken)
            ?? throw new SpeakingNotFoundException("Không tìm thấy đề Speaking.");
        var draftExerciseIds = loaded.Slots.Where(slot => slot.SnapshotJson is null)
            .Select(slot => slot.ExerciseId).Distinct().ToArray();
        var exercises = draftExerciseIds.Length == 0
            ? new Dictionary<Guid, Domain.Entities.SpeakingExercise>()
            : (await repository.FindExercisesAsync(draftExerciseIds, cancellationToken))
            .ToDictionary(exercise => exercise.Id);
        return loaded.ToDetailDto(exercises);
    }
}
