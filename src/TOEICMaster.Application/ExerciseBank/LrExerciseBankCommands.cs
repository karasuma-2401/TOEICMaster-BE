using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public sealed record CreateLrExerciseItemCommand(Guid ActorUserId, int Part, LrExerciseItemInput Input)
    : IRequest<LrExerciseItemDto>;

public sealed record ImportLrExerciseItemsCommand(Guid ActorUserId, int Part, string FileName, byte[] Content)
    : IRequest<LrExerciseImportResultDto>;

public sealed class LrExerciseItemInputValidator : AbstractValidator<LrExerciseItemInput>
{
    public LrExerciseItemInputValidator()
    {
        RuleFor(input => input.Code).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(64);
        RuleFor(input => input.Topic).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(100);
        RuleFor(input => input.Difficulty).Must(value => value is not null &&
            new[] { "EASY", "MEDIUM", "HARD" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase));
        RuleFor(input => input.Status).Must(value => value is not null &&
            new[] { "DRAFT", "PUBLISHED", "ARCHIVED" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase));
        RuleFor(input => input.Context).MaximumLength(12_000);
        RuleFor(input => input.AudioUrl).MaximumLength(2_000);
        RuleFor(input => input.ImageUrl).MaximumLength(2_000);
        RuleFor(input => input.Questions).NotEmpty().Must(questions => questions.Count <= 10);
        RuleForEach(input => input.Questions).ChildRules(question =>
        {
            question.RuleFor(value => value.Code).NotEmpty().MaximumLength(64);
            question.RuleFor(value => value.Prompt).NotEmpty().MaximumLength(4_000);
            question.RuleFor(value => value.OptionA).NotEmpty().MaximumLength(1_000);
            question.RuleFor(value => value.OptionB).NotEmpty().MaximumLength(1_000);
            question.RuleFor(value => value.OptionC).NotEmpty().MaximumLength(1_000);
            question.RuleFor(value => value.OptionD).MaximumLength(1_000);
            question.RuleFor(value => value.Translation).MaximumLength(4_000);
            question.RuleFor(value => value.Explanation).MaximumLength(4_000);
        });
    }
}

public sealed class CreateLrExerciseItemCommandValidator : AbstractValidator<CreateLrExerciseItemCommand>
{
    public CreateLrExerciseItemCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Part).InclusiveBetween(1, 7);
        RuleFor(command => command.Input).SetValidator(new LrExerciseItemInputValidator());
    }
}

public sealed class ImportLrExerciseItemsCommandValidator : AbstractValidator<ImportLrExerciseItemsCommand>
{
    public ImportLrExerciseItemsCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Part).InclusiveBetween(1, 7);
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255);
        RuleFor(command => command.Content).NotEmpty().Must(content => content.Length <= 10 * 1024 * 1024);
    }
}

public sealed class CreateLrExerciseItemCommandHandler(ILrExerciseBankRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateLrExerciseItemCommand, LrExerciseItemDto>
{
    public async Task<LrExerciseItemDto> Handle(CreateLrExerciseItemCommand request, CancellationToken cancellationToken)
    {
        if (await repository.ItemCodeExistsAsync(request.Input.Code.Trim(), cancellationToken))
            throw new LrExerciseBankConflictException("Mã cụm bài tập đã tồn tại.");
        LrExerciseBankItem item;
        try
        {
            item = request.Input.ToEntity(request.Part, request.ActorUserId, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new InvalidLrExerciseBankItemException(exception.Message);
        }
        await repository.AddItemsAsync([item], cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return item.ToDto();
    }
}

public sealed class ImportLrExerciseItemsCommandHandler(ILrExerciseBankRepository repository,
    ILrExerciseBankImportParser parser, TimeProvider timeProvider)
    : IRequestHandler<ImportLrExerciseItemsCommand, LrExerciseImportResultDto>
{
    public async Task<LrExerciseImportResultDto> Handle(ImportLrExerciseItemsCommand request,
        CancellationToken cancellationToken)
    {
        var inputs = parser.Parse(request.Part, request.FileName, request.Content);
        if (inputs.Count == 0)
            throw new LrExerciseBankImportException("Workbook chưa có dòng dữ liệu để import.");
        var duplicateCodes = inputs.GroupBy(input => input.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateCodes.Length > 0)
            throw new LrExerciseBankImportException($"Trùng mã cụm trong workbook: {string.Join(", ", duplicateCodes)}.");
        foreach (var input in inputs)
            if (await repository.ItemCodeExistsAsync(input.Code.Trim(), cancellationToken))
                throw new LrExerciseBankConflictException($"Mã cụm bài tập '{input.Code.Trim()}' đã tồn tại.");

        var now = timeProvider.GetUtcNow();
        LrExerciseBankItem[] items;
        try
        {
            items = inputs.Select(input => input.ToEntity(request.Part, request.ActorUserId, now)).ToArray();
        }
        catch (ArgumentException exception)
        {
            throw new LrExerciseBankImportException(exception.Message);
        }
        await repository.AddItemsAsync(items, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var summary = await repository.GetSummaryAsync(cancellationToken);
        return new LrExerciseImportResultDto(request.Part, items.Length, items.Sum(item => item.Questions.Count), summary);
    }
}
