using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record PreviewWritingExerciseImportCommand(int Part, string FileName, byte[] Content)
    : IRequest<WritingExerciseImportPreviewDto>;

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
