using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.CreateMediaUploadIntent;

public sealed record CreateMediaUploadIntentCommand(Guid ExamId, Guid RevisionId, ExamMediaOwnerKind OwnerKind,
    string OwnerKey, string ContentType, long ByteSize) : IRequest<ExamMediaUploadIntentDto>;

public sealed class CreateMediaUploadIntentCommandValidator : AbstractValidator<CreateMediaUploadIntentCommand>
{
    public CreateMediaUploadIntentCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();
        RuleFor(command => command.RevisionId).NotEmpty();
        RuleFor(command => command.OwnerKind).IsInEnum();
        RuleFor(command => command.OwnerKey).NotEmpty().MaximumLength(128);
        RuleFor(command => command.ContentType).NotEmpty().MaximumLength(64);
        RuleFor(command => command.ByteSize).GreaterThan(0);
    }
}

public sealed class CreateMediaUploadIntentCommandHandler(IExamRepository repository, IExamMediaStore store,
    TimeProvider timeProvider) : IRequestHandler<CreateMediaUploadIntentCommand, ExamMediaUploadIntentDto>
{
    private static readonly IReadOnlyDictionary<string, (ExamMediaKind Kind, string Extension, long MaxBytes)> AllowedMedia =
        new Dictionary<string, (ExamMediaKind, string, long)>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio/mpeg"] = (ExamMediaKind.Audio, "mp3", 100 * 1024 * 1024),
            ["audio/wav"] = (ExamMediaKind.Audio, "wav", 100 * 1024 * 1024),
            ["image/png"] = (ExamMediaKind.Image, "png", 5 * 1024 * 1024),
            ["image/jpeg"] = (ExamMediaKind.Image, "jpg", 5 * 1024 * 1024),
            ["image/webp"] = (ExamMediaKind.Image, "webp", 5 * 1024 * 1024)
        };

    public async Task<ExamMediaUploadIntentDto> Handle(CreateMediaUploadIntentCommand request,
        CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        if (revision.Status != ExamRevisionStatus.Draft || !revision.UsesMediaWorkspace)
            throw new ExamMediaUploadIntentConflictException("This revision does not use the media workspace.");
        if (!AllowedMedia.TryGetValue(request.ContentType, out var media) || request.ByteSize > media.MaxBytes ||
            !IsOwnerValid(revision, request.OwnerKind, request.OwnerKey, media.Kind))
            throw new InvalidExamMediaException();

        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(15);
        var objectKey = $"exam-media/{revision.Id:N}/{Guid.NewGuid():N}.{media.Extension}";
        var intent = ExamMediaUploadIntent.Create(exam.Id, revision.Id, request.OwnerKind, request.OwnerKey, objectKey,
            request.ContentType.ToLowerInvariant(), request.ByteSize, expiresAtUtc, now);
        var uploadUrl = await store.CreatePresignedPutUrlAsync(objectKey, expiresAtUtc - now, cancellationToken);
        await repository.AddExamMediaUploadIntentAsync(intent, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ExamMediaUploadIntentDto.From(intent, uploadUrl);
    }

    private static bool IsOwnerValid(ExamRevision revision, ExamMediaOwnerKind ownerKind, string ownerKey,
        ExamMediaKind mediaKind)
    {
        if (ownerKind == ExamMediaOwnerKind.AudioGroup)
            return mediaKind == ExamMediaKind.Audio && revision.Blocks.Any(block => block.Key == ownerKey && block.Part is >= 1 and <= 4);
        if (ownerKind == ExamMediaOwnerKind.QuestionImage)
            return mediaKind == ExamMediaKind.Image && int.TryParse(ownerKey, out var number) && revision.Blocks
                .Where(block => block.Part >= 5).SelectMany(block => block.Questions).Any(question => question.Number == number);
        if (ownerKind != ExamMediaOwnerKind.ContextImage || mediaKind != ExamMediaKind.Image)
            return false;
        var separator = ownerKey.LastIndexOf(':');
        return separator > 0 && int.TryParse(ownerKey[(separator + 1)..], out var ordinal) && revision.Blocks
            .Any(block => block.Key == ownerKey[..separator] && block.Part >= 5 && block.ContextBlocks.Any(context =>
                context.Ordinal == ordinal && context.Type == ExamContextType.Image));
    }
}
