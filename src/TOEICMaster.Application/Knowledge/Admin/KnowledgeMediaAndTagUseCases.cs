using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Knowledge.Admin;

public sealed record GetAdminKnowledgeTagsQuery(KnowledgeAdminTagFilter Filter) : IRequest<KnowledgeTagPageDto>;

public sealed class GetAdminKnowledgeTagsQueryValidator : AbstractValidator<GetAdminKnowledgeTagsQuery>
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "name:asc", "name:desc", "createdAt:desc", "createdAt:asc"
    };

    public GetAdminKnowledgeTagsQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Filter.Query).MaximumLength(50);
            RuleFor(x => x.Filter.Sort)
                .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSorts.Contains(sort.Trim()))
                .WithMessage("Kiểu sắp xếp thẻ không hợp lệ.");
        });
    }
}

public sealed class GetAdminKnowledgeTagsQueryHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<GetAdminKnowledgeTagsQuery, KnowledgeTagPageDto>
{
    public Task<KnowledgeTagPageDto> Handle(GetAdminKnowledgeTagsQuery request,
        CancellationToken cancellationToken) =>
        repository.GetTagsPageAsync(request.Filter, cancellationToken);
}

public sealed record UploadKnowledgeImageCommand(Guid ActorUserId, byte[] Content, string FileName)
    : IRequest<KnowledgeMediaAssetDto>;

public sealed class UploadKnowledgeImageCommandHandler(
    IKnowledgeAdminRepository repository,
    IKnowledgeMediaStore mediaStore,
    TimeProvider timeProvider) : IRequestHandler<UploadKnowledgeImageCommand, KnowledgeMediaAssetDto>
{
    public async Task<KnowledgeMediaAssetDto> Handle(UploadKnowledgeImageCommand request,
        CancellationToken cancellationToken)
    {
        var validated = KnowledgeImageSignature.Validate(request.Content);
        var safeFileName = string.IsNullOrWhiteSpace(request.FileName)
            ? $"image{validated.Extension}"
            : Path.GetFileName(request.FileName.Trim());
        if (safeFileName.Length > 255)
            safeFileName = safeFileName[..255];

        var objectKey = $"knowledge/{Guid.NewGuid():N}{validated.Extension}";
        await mediaStore.UploadAsync(objectKey, request.Content, validated.ContentType, cancellationToken);

        var asset = KnowledgeMediaAsset.Create(objectKey, safeFileName, validated.ContentType,
            request.Content.Length, validated.Sha256, request.ActorUserId, timeProvider.GetUtcNow());
        try
        {
            await repository.AddMediaAssetAsync(asset, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteOrphanAsync(objectKey);
            throw;
        }

        return new KnowledgeMediaAssetDto(asset.Id, asset.OriginalFileName, asset.ContentType, asset.ByteLength,
            KnowledgeEnumParsing.ToAdminMediaUrl(asset.Id), asset.CreatedAtUtc);
    }

    private async Task TryDeleteOrphanAsync(string objectKey)
    {
        try
        {
            await mediaStore.DeleteAsync(objectKey, CancellationToken.None);
        }
        catch
        {
            // Best-effort cleanup when DB persistence fails after storage upload.
        }
    }
}

public sealed record GetAdminKnowledgeImageQuery(Guid AssetId) : IRequest<KnowledgeMediaBinary>;

public sealed class GetAdminKnowledgeImageQueryHandler(
    IKnowledgeAdminRepository repository,
    IKnowledgeMediaStore mediaStore) : IRequestHandler<GetAdminKnowledgeImageQuery, KnowledgeMediaBinary>
{
    public async Task<KnowledgeMediaBinary> Handle(GetAdminKnowledgeImageQuery request,
        CancellationToken cancellationToken)
    {
        var asset = await repository.FindMediaAssetAsync(request.AssetId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy hình ảnh kiến thức.");
        var bytes = await mediaStore.DownloadAsync(asset.ObjectKey, cancellationToken);
        return new KnowledgeMediaBinary(bytes, asset.ContentType);
    }
}

public sealed record GetAdminKnowledgeMediaAssetsQuery(KnowledgeAdminMediaFilter Filter)
    : IRequest<KnowledgeMediaAssetPageDto>;

public sealed class GetAdminKnowledgeMediaAssetsQueryValidator : AbstractValidator<GetAdminKnowledgeMediaAssetsQuery>
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt:desc", "createdAt:asc", "fileName:asc", "fileName:desc", "byteLength:desc", "byteLength:asc"
    };

    public GetAdminKnowledgeMediaAssetsQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Filter.Query).MaximumLength(255);
            RuleFor(x => x.Filter.Sort)
                .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSorts.Contains(sort.Trim()))
                .WithMessage("Kiểu sắp xếp hình ảnh không hợp lệ.");
        });
    }
}

public sealed class GetAdminKnowledgeMediaAssetsQueryHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<GetAdminKnowledgeMediaAssetsQuery, KnowledgeMediaAssetPageDto>
{
    public Task<KnowledgeMediaAssetPageDto> Handle(GetAdminKnowledgeMediaAssetsQuery request,
        CancellationToken cancellationToken) =>
        repository.GetMediaAssetPageAsync(request.Filter, cancellationToken);
}

public sealed record DeleteKnowledgeMediaAssetCommand(Guid AssetId) : IRequest;

public sealed class DeleteKnowledgeMediaAssetCommandValidator : AbstractValidator<DeleteKnowledgeMediaAssetCommand>
{
    public DeleteKnowledgeMediaAssetCommandValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
    }
}

public sealed class DeleteKnowledgeMediaAssetCommandHandler(
    IKnowledgeAdminRepository repository,
    IKnowledgeMediaStore mediaStore) : IRequestHandler<DeleteKnowledgeMediaAssetCommand>
{
    public async Task Handle(DeleteKnowledgeMediaAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await repository.FindMediaAssetForDeleteAsync(request.AssetId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy hình ảnh kiến thức.");

        if (await repository.IsMediaAssetUsedAsync(asset.Id, cancellationToken))
            throw new KnowledgeConflictException(
                "Không thể xóa hình ảnh đang được sử dụng trong bài viết. Hãy gỡ ảnh khỏi các bài viết liên quan trước.");

        var objectKey = asset.ObjectKey;
        repository.RemoveMediaAsset(asset);
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            await mediaStore.DeleteAsync(objectKey, cancellationToken);
        }
        catch
        {
            // Best-effort storage cleanup after DB row is already deleted.
        }
    }
}
