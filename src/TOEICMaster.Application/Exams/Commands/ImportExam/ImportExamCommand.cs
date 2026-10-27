using FluentValidation;
using MediatR;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;
using TOEICMaster.Application.Exams.Importing;

namespace TOEICMaster.Application.Exams.Commands.ImportExam;

public sealed record ImportExamCommand(byte[] WorkbookContent, byte[] MasterAudioContent, string MasterAudioContentType)
    : IRequest<ExamDraftDto>;

public sealed class ImportExamCommandValidator : AbstractValidator<ImportExamCommand>
{
    public ImportExamCommandValidator()
    {
        RuleFor(command => command.WorkbookContent).NotEmpty().Must(content => content.Length <= XlsxExamWorkbookReader.MaxBytes);
        RuleFor(command => command.MasterAudioContent).NotEmpty()
            .Must(content => content.Length <= UploadMasterAudio.UploadMasterAudioCommandHandler.MaxBytes);
        RuleFor(command => command.MasterAudioContentType).NotEmpty().MaximumLength(64);
    }
}

public sealed class ImportExamCommandHandler(IExamRepository repository, IExamMediaStore store, TimeProvider timeProvider)
    : IRequestHandler<ImportExamCommand, ExamDraftDto>
{
    public async Task<ExamDraftDto> Handle(ImportExamCommand request, CancellationToken cancellationToken)
    {
        var workbook = XlsxExamWorkbookReader.Read(request.WorkbookContent);
        if (await repository.ExamCodeExistsAsync(workbook.TestCode.Trim(), cancellationToken))
            throw new ExamConflictException();
        if (await repository.FindScoreProfileAsync(workbook.ScoreProfileId, cancellationToken) is null)
            throw new ExamNotFoundException();
        if (!await repository.ImageAssetsExistAsync(ImportedExamDraftBuilder.GetReferencedImageAssetIds(workbook),
                cancellationToken))
            throw new ExamWorkbookFormatException(["Workbook tham chiếu ImageAssetId chưa tồn tại."]);

        var media = ExamMediaInspector.DetectMasterAudio(request.MasterAudioContent);
        if (request.MasterAudioContent.Length == 0 ||
            request.MasterAudioContent.Length > UploadMasterAudio.UploadMasterAudioCommandHandler.MaxBytes || media is null ||
            !ExamMediaInspector.MatchesClaimedContentType(request.MasterAudioContentType, media.Value.ContentType))
            throw new InvalidExamMediaException();

        var pendingAudioKey = $"exam-audio/pending/{Guid.NewGuid():N}.{media.Value.Extension}";
        var exam = ImportedExamDraftBuilder.Build(workbook, pendingAudioKey, timeProvider.GetUtcNow());
        var revision = exam.Revisions.Single();
        var objectKey = $"exam-audio/{revision.Id:N}/{Guid.NewGuid():N}.{media.Value.Extension}";
        await store.UploadAsync(objectKey, request.MasterAudioContent, media.Value.ContentType, cancellationToken);
        try
        {
            revision.SetMasterAudioObjectKey(objectKey);
            await repository.AddExamAsync(exam, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return ExamDraftDto.From(exam, revision);
        }
        catch
        {
            try { await store.DeleteAsync(objectKey, cancellationToken); }
            catch { /* A failed atomic import must not intentionally retain its uploaded master audio. */ }
            throw;
        }
    }
}
