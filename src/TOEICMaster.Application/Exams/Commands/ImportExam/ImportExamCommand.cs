using FluentValidation;
using MediatR;
using TOEICMaster.Application.Exams.Importing;

namespace TOEICMaster.Application.Exams.Commands.ImportExam;

public sealed record ImportExamCommand(byte[] WorkbookContent) : IRequest<ExamDraftDto>;

public sealed class ImportExamCommandValidator : AbstractValidator<ImportExamCommand>
{
    public ImportExamCommandValidator()
    {
        RuleFor(command => command.WorkbookContent).NotEmpty().Must(content => content.Length <= XlsxExamWorkbookReader.MaxBytes);
    }
}

public sealed class ImportExamCommandHandler(IExamRepository repository, IExamMediaStore store, TimeProvider timeProvider)
    : IRequestHandler<ImportExamCommand, ExamDraftDto>
{
    public async Task<ExamDraftDto> Handle(ImportExamCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var workbook = XlsxExamWorkbookReader.Read(request.WorkbookContent);
        if (await repository.ExamCodeExistsAsync(workbook.TestCode.Trim(), cancellationToken))
            throw new ExamConflictException();
        if (await repository.FindScoreProfileAsync(workbook.ScoreProfileId, cancellationToken) is null)
            throw new ExamNotFoundException();
        if (!await repository.ImageAssetsExistAsync(ImportedExamDraftBuilder.GetReferencedImageAssetIds(workbook),
                cancellationToken))
            throw new ExamWorkbookFormatException(["Workbook tham chiếu ImageAssetId chưa tồn tại."]);

        var exam = ImportedExamDraftBuilder.Build(workbook, timeProvider.GetUtcNow());
        await repository.AddExamAsync(exam, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ExamDraftDto.From(exam, exam.Revisions.Single());
    }
}
