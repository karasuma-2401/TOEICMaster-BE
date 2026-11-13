using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.CompleteMediaUploadIntent;

public sealed record CompleteMediaUploadIntentCommand(Guid ExamId, Guid RevisionId, Guid IntentId)
    : IRequest<ExamMediaAssetDto>;

public sealed class CompleteMediaUploadIntentCommandHandler(IExamRepository repository, IExamMediaStore store,
    TimeProvider timeProvider) : IRequestHandler<CompleteMediaUploadIntentCommand, ExamMediaAssetDto>
{
    public async Task<ExamMediaAssetDto> Handle(CompleteMediaUploadIntentCommand request, CancellationToken cancellationToken)
    {
        var intent = await repository.FindExamMediaUploadIntentAsync(request.IntentId, cancellationToken)
            ?? throw new ExamNotFoundException();
        if (intent.ExamId != request.ExamId || intent.RevisionId != request.RevisionId)
            throw new ExamNotFoundException();
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        if (revision.Status != ExamRevisionStatus.Draft || !revision.UsesMediaWorkspace)
            throw new ExamMediaUploadIntentConflictException("This revision does not use the media workspace.");

        var now = timeProvider.GetUtcNow();
        if (intent.Status != ExamMediaUploadIntentStatus.Pending || now > intent.ExpiresAtUtc)
            throw new ExamMediaUploadIntentConflictException("The upload intent is no longer active.");
        var metadata = await store.GetMetadataAsync(intent.ObjectKey, cancellationToken);
        if (metadata is null || metadata.ByteSize != intent.ExpectedByteSize || !string.Equals(metadata.ContentType,
                intent.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidExamMediaException();
        intent.Complete(now);

        var kind = intent.OwnerKind == ExamMediaOwnerKind.AudioGroup ? ExamMediaKind.Audio : ExamMediaKind.Image;
        var asset = ExamMediaAsset.Create(intent.ObjectKey, intent.ContentType, now, kind, metadata.ByteSize);
        switch (intent.OwnerKind)
        {
            case ExamMediaOwnerKind.AudioGroup:
                revision.AttachAudioAsset(intent.OwnerKey, asset.Id);
                break;
            case ExamMediaOwnerKind.QuestionImage:
                revision.AttachQuestionImageAsset(int.Parse(intent.OwnerKey), asset.Id);
                break;
            case ExamMediaOwnerKind.ContextImage:
                var separator = intent.OwnerKey.LastIndexOf(':');
                revision.AttachContextImageAsset(intent.OwnerKey[..separator], int.Parse(intent.OwnerKey[(separator + 1)..]), asset.Id);
                break;
            default:
                throw new InvalidExamMediaException();
        }

        await repository.AddExamMediaAssetAsync(asset, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ExamMediaAssetDto.From(asset);
    }
}
