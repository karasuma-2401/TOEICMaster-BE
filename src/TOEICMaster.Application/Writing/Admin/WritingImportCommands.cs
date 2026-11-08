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
            throw new InvalidWritingImportException("File có dữ liệu không hợp lệ hoặc không có dòng để import.");
        var inputs = preview.Rows.Select(row => row.Input!.Normalize()).ToArray();
        if (inputs.Select(input => input.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Length)
            throw new WritingConflictException("File có mã bài tập trùng nhau.");
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await repository.ExerciseCodeExistsAsync(input.Code, null, cancellationToken))
                throw new WritingConflictException($"Mã bài {input.Code} đã tồn tại.");
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
            .WithMessage("File import phải có định dạng .xlsx.");
        RuleFor(command => command.Content).NotEmpty().Must(content => content.Length <= 10 * 1024 * 1024)
            .WithMessage("File import không được vượt quá 10 MiB.");
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
