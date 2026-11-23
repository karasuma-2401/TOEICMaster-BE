using FluentValidation;
using MediatR;
using TOEICMaster.Application.Common.Exceptions;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record PreviewWritingExerciseImportCommand(int Part, string FileName, byte[] Content)
    : IRequest<WritingExerciseImportPreviewDto>;

public sealed record ImportWritingExercisesCommand(Guid ActorUserId, int Part, string FileName, byte[] Content)
    : IRequest<int>;

public sealed class ImportWritingExercisesCommandValidator : AbstractValidator<ImportWritingExercisesCommand>
{
    public ImportWritingExercisesCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => new PreviewWritingExerciseImportCommand(command.Part, command.FileName, command.Content))
            .SetValidator(new PreviewWritingExerciseImportCommandValidator());
    }
}

public sealed class ImportWritingExercisesCommandHandler(IWritingExerciseImportParser parser,
    IWritingAdminRepository repository, TimeProvider timeProvider) : IRequestHandler<ImportWritingExercisesCommand, int>
{
    public async Task<int> Handle(ImportWritingExercisesCommand request, CancellationToken cancellationToken)
    {
        var preview = parser.Preview(request.Part, request.FileName, request.Content);
        if (preview.Rows.Count == 0 || preview.InvalidRowCount > 0 || preview.Rows.Any(row => row.Input is null))
            throw new InvalidWritingImportException("The file contains invalid data or has no rows to import.");
        var inputs = preview.Rows.Select(row => row.Input!.Normalize()).ToArray();
        if (inputs.Select(input => input.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Length)
            throw new WritingConflictException("The file contains duplicate exercise codes.");
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await repository.ExerciseCodeExistsAsync(input.Code, null, cancellationToken))
                throw new WritingConflictException($"Exercise code {input.Code} already exists.");
            await CreateWritingExerciseCommandHandler.EnsureImageExistsAsync(input, repository, cancellationToken);
        }
        var now = timeProvider.GetUtcNow();
        foreach (var input in inputs)
            await repository.AddExerciseAsync(input.ToEntity(request.ActorUserId, now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return inputs.Length;
    }
}

public sealed class PreviewWritingExerciseImportCommandValidator : AbstractValidator<PreviewWritingExerciseImportCommand>
{
    public PreviewWritingExerciseImportCommandValidator()
    {
        RuleFor(command => command.Part).InclusiveBetween(1, 3);
        RuleFor(command => command.FileName).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(255)
            .Must(name => name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("The import file must be in .xlsx format.");
        RuleFor(command => command.Content).NotEmpty().Must(content => content.Length <= 10 * 1024 * 1024)
            .WithMessage("The import file must not exceed 10 MiB.");
    }
}

public sealed class PreviewWritingExerciseImportCommandHandler(IWritingExerciseImportParser parser)
    : IRequestHandler<PreviewWritingExerciseImportCommand, WritingExerciseImportPreviewDto>
{
    public Task<WritingExerciseImportPreviewDto> Handle(PreviewWritingExerciseImportCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(parser.Preview(request.Part, request.FileName, request.Content));
    }
}
