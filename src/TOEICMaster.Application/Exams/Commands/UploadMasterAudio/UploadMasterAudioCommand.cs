using MediatR;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;

namespace TOEICMaster.Application.Exams.Commands.UploadMasterAudio;

public sealed record UploadMasterAudioCommand(Guid ExamId, Guid RevisionId, byte[] Content, string ContentType)
    : IRequest;

public sealed class UploadMasterAudioCommandHandler(IExamRepository repository, IExamMediaStore store)
    : IRequestHandler<UploadMasterAudioCommand>
{
    public const int MaxBytes = 100 * 1024 * 1024;

    public async Task Handle(UploadMasterAudioCommand request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        var media = ExamMediaInspector.DetectMasterAudio(request.Content);
        if (request.Content.Length == 0 || request.Content.Length > MaxBytes || media is null ||
            !ExamMediaInspector.MatchesClaimedContentType(request.ContentType, media.Value.ContentType))
            throw new InvalidExamMediaException();

        var objectKey = $"exam-audio/{revision.Id:N}/{Guid.NewGuid():N}.{media.Value.Extension}";
        await store.UploadAsync(objectKey, request.Content, media.Value.ContentType, cancellationToken);
        var oldObjectKey = revision.MasterAudioObjectKey;
        try
        {
            revision.SetMasterAudioObjectKey(objectKey);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try { await store.DeleteAsync(objectKey, cancellationToken); }
            catch { /* The database change failed; best-effort cleanup prevents an orphaned object. */ }
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldObjectKey))
        {
            try { await store.DeleteAsync(oldObjectKey, cancellationToken); }
            catch { /* The new master audio is already persisted; old-object cleanup is best effort. */ }
        }
    }
}
