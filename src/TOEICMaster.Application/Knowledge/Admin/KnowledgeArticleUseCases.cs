using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Knowledge.Admin;

public sealed record GetAdminKnowledgeArticlesQuery(KnowledgeAdminArticleFilter Filter)
    : IRequest<KnowledgeArticlePageDto>;

public sealed class GetAdminKnowledgeArticlesQueryValidator : AbstractValidator<GetAdminKnowledgeArticlesQuery>
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "updatedAt:desc", "updatedAt:asc", "createdAt:desc", "createdAt:asc",
        "publishedAt:desc", "publishedAt:asc", "title:asc", "title:desc"
    };

    public GetAdminKnowledgeArticlesQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Filter.Query).MaximumLength(200);
            RuleFor(x => x.Filter.Tag).MaximumLength(64);
            RuleFor(x => x.Filter.Skill)
                .Must(skill => string.IsNullOrWhiteSpace(skill) || KnowledgeEnumParsing.TryParseSkill(skill, out _))
                .WithMessage("Kỹ năng không hợp lệ. Giá trị hợp lệ: General, Listening, Reading, Speaking, Writing.");
            RuleFor(x => x.Filter.Status)
                .Must(status => string.IsNullOrWhiteSpace(status) || KnowledgeEnumParsing.TryParseStatus(status, out _))
                .WithMessage("Trạng thái không hợp lệ. Giá trị hợp lệ: Draft, Published, Archived.");
            RuleFor(x => x.Filter.Sort)
                .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSorts.Contains(sort.Trim()))
                .WithMessage("Kiểu sắp xếp không hợp lệ.");
        });
    }
}

public sealed class GetAdminKnowledgeArticlesQueryHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<GetAdminKnowledgeArticlesQuery, KnowledgeArticlePageDto>
{
    public Task<KnowledgeArticlePageDto> Handle(GetAdminKnowledgeArticlesQuery request,
        CancellationToken cancellationToken) =>
        repository.GetArticlePageAsync(request.Filter, cancellationToken);
}

public sealed record GetAdminKnowledgeArticleQuery(Guid ArticleId) : IRequest<KnowledgeArticleDetailDto>;

public sealed class GetAdminKnowledgeArticleQueryHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<GetAdminKnowledgeArticleQuery, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(GetAdminKnowledgeArticleQuery request,
        CancellationToken cancellationToken) =>
        await repository.GetArticleDetailAsync(request.ArticleId, cancellationToken)
        ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
}

internal sealed class KnowledgeArticleInputValidator : AbstractValidator<KnowledgeArticleInput>
{
    public KnowledgeArticleInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220)
            .Must(slug => string.IsNullOrWhiteSpace(slug) ||
                          KnowledgeSlug.IsValid(slug.Trim().ToLowerInvariant(), 220))
            .WithMessage("Slug chỉ gồm chữ thường không dấu, số và dấu gạch ngang.");
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Skill).NotEmpty()
            .Must(skill => KnowledgeEnumParsing.TryParseSkill(skill, out _))
            .WithMessage("Kỹ năng không hợp lệ. Giá trị hợp lệ: General, Listening, Reading, Speaking, Writing.");
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.CoverImageAlt).MaximumLength(300);
        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 10)
            .WithMessage("Mỗi bài viết chỉ được gắn tối đa 10 thẻ (tags).");
        RuleForEach(x => x.Tags)
            .NotEmpty().MaximumLength(50)
            .When(x => x.Tags is not null);
    }
}

public sealed record CreateKnowledgeArticleCommand(Guid ActorUserId, KnowledgeArticleInput Input)
    : IRequest<KnowledgeArticleDetailDto>;

public sealed class CreateKnowledgeArticleCommandValidator : AbstractValidator<CreateKnowledgeArticleCommand>
{
    public CreateKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new KnowledgeArticleInputValidator());
    }
}

public sealed class CreateKnowledgeArticleCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<CreateKnowledgeArticleCommand, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(CreateKnowledgeArticleCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Input;
        if (!KnowledgeEnumParsing.TryParseSkill(input.Skill, out var skill))
            throw new InvalidKnowledgeContentException("Kỹ năng bài viết không hợp lệ.");

        if (!await repository.CategoryExistsAsync(input.CategoryId, cancellationToken))
            throw new KnowledgeNotFoundException("Không tìm thấy danh mục kiến thức được chọn.");

        var slug = KnowledgeSlug.Resolve(input.Slug, input.Title, 220);
        if (!KnowledgeSlug.IsValid(slug, 220))
            throw new InvalidKnowledgeContentException("Không thể tạo slug hợp lệ từ tiêu đề. Hãy nhập slug thủ công.");

        if (await repository.ArticleSlugExistsAsync(slug, null, cancellationToken))
            throw new KnowledgeConflictException($"Slug bài viết '{slug}' đã tồn tại.");

        var processed = KnowledgeContentProcessor.Process(input.Content);
        await EnsureMediaAssetsExistAsync(repository, input.CoverImageAssetId, processed.EmbeddedMediaAssetIds,
            cancellationToken);

        var now = timeProvider.GetUtcNow();
        var tagIds = await ResolveTagsAsync(repository, input.Tags, now, cancellationToken);

        var article = KnowledgeArticle.Create(input.Title, slug, input.Summary, skill, input.CategoryId,
            input.CoverImageAssetId, input.CoverImageAlt, processed.ContentJson, processed.TocJson,
            processed.EstimatedReadMinutes, tagIds, processed.EmbeddedMediaAssetIds, request.ActorUserId, now);

        await repository.AddArticleAsync(article, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return await repository.GetArticleDetailAsync(article.Id, cancellationToken)
               ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức vừa tạo.");
    }

    internal static async Task EnsureMediaAssetsExistAsync(IKnowledgeAdminRepository repository,
        Guid? coverImageAssetId, IReadOnlyList<Guid> embeddedIds, CancellationToken cancellationToken)
    {
        var requested = new HashSet<Guid>(embeddedIds);
        if (coverImageAssetId is { } coverId && coverId != Guid.Empty)
            requested.Add(coverId);
        if (requested.Count == 0)
            return;

        var existing = await repository.FindExistingMediaAssetIdsAsync(requested, cancellationToken);
        if (existing.Count != requested.Count)
            throw new InvalidKnowledgeContentException("Có hình ảnh tham chiếu không tồn tại trong hệ thống.");
    }

    internal static async Task<IReadOnlyList<Guid>> ResolveTagsAsync(IKnowledgeAdminRepository repository,
        IReadOnlyList<string>? rawTags, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (rawTags is null || rawTags.Count == 0)
            return [];

        var normalizedBySlug = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in rawTags)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var name = raw.Trim();
            var slug = KnowledgeSlug.Generate(name, 64);
            if (!KnowledgeSlug.IsValid(slug, 64))
                throw new InvalidKnowledgeContentException($"Thẻ '{name}' không hợp lệ.");
            normalizedBySlug.TryAdd(slug, name);
        }

        if (normalizedBySlug.Count == 0)
            return [];

        var existing = await repository.FindTagsBySlugsAsync(normalizedBySlug.Keys.ToArray(), cancellationToken);
        var existingBySlug = existing.ToDictionary(tag => tag.Slug, StringComparer.Ordinal);
        var toCreate = new List<KnowledgeTag>();
        var resultIds = new List<Guid>(normalizedBySlug.Count);

        foreach (var (slug, name) in normalizedBySlug)
        {
            if (existingBySlug.TryGetValue(slug, out var found))
            {
                resultIds.Add(found.Id);
            }
            else
            {
                var created = KnowledgeTag.Create(name, slug, now);
                toCreate.Add(created);
                resultIds.Add(created.Id);
            }
        }

        if (toCreate.Count > 0)
            await repository.AddTagsAsync(toCreate, cancellationToken);

        return resultIds;
    }
}

public sealed record UpdateKnowledgeArticleCommand(
    Guid ActorUserId,
    Guid ArticleId,
    int Version,
    KnowledgeArticleInput Input) : IRequest<KnowledgeArticleDetailDto>;

public sealed class UpdateKnowledgeArticleCommandValidator : AbstractValidator<UpdateKnowledgeArticleCommand>
{
    public UpdateKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Input).NotNull().SetValidator(new KnowledgeArticleInputValidator());
    }
}

public sealed class UpdateKnowledgeArticleCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<UpdateKnowledgeArticleCommand, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(UpdateKnowledgeArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await repository.FindArticleForUpdateAsync(request.ArticleId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
        if (article.Version != request.Version)
            throw new KnowledgeConflictException("Bài viết đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");

        var input = request.Input;
        if (!KnowledgeEnumParsing.TryParseSkill(input.Skill, out var skill))
            throw new InvalidKnowledgeContentException("Kỹ năng bài viết không hợp lệ.");

        if (!await repository.CategoryExistsAsync(input.CategoryId, cancellationToken))
            throw new KnowledgeNotFoundException("Không tìm thấy danh mục kiến thức được chọn.");

        var slug = KnowledgeSlug.Resolve(input.Slug, input.Title, 220);
        if (!KnowledgeSlug.IsValid(slug, 220))
            throw new InvalidKnowledgeContentException("Không thể tạo slug hợp lệ từ tiêu đề. Hãy nhập slug thủ công.");

        if (await repository.ArticleSlugExistsAsync(slug, article.Id, cancellationToken))
            throw new KnowledgeConflictException($"Slug bài viết '{slug}' đã tồn tại.");

        var processed = KnowledgeContentProcessor.Process(input.Content);
        await CreateKnowledgeArticleCommandHandler.EnsureMediaAssetsExistAsync(repository, input.CoverImageAssetId,
            processed.EmbeddedMediaAssetIds, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var tagIds = await CreateKnowledgeArticleCommandHandler.ResolveTagsAsync(repository, input.Tags, now,
            cancellationToken);

        article.Update(input.Title, slug, input.Summary, skill, input.CategoryId, input.CoverImageAssetId,
            input.CoverImageAlt, processed.ContentJson, processed.TocJson, processed.EstimatedReadMinutes, tagIds,
            processed.EmbeddedMediaAssetIds, request.ActorUserId, now);

        await repository.SaveChangesAsync(cancellationToken);

        return await repository.GetArticleDetailAsync(article.Id, cancellationToken)
               ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
    }
}

public sealed record PublishKnowledgeArticleCommand(Guid ActorUserId, Guid ArticleId, int Version)
    : IRequest<KnowledgeArticleDetailDto>;

public sealed class PublishKnowledgeArticleCommandValidator : AbstractValidator<PublishKnowledgeArticleCommand>
{
    public PublishKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class PublishKnowledgeArticleCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<PublishKnowledgeArticleCommand, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(PublishKnowledgeArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await repository.FindArticleForUpdateAsync(request.ArticleId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
        if (article.Version != request.Version)
            throw new KnowledgeConflictException("Bài viết đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        if (article.Status == KnowledgeArticleStatus.Published)
            throw new KnowledgeConflictException("Bài viết đã ở trạng thái xuất bản.");

        article.Publish(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return await repository.GetArticleDetailAsync(article.Id, cancellationToken)
               ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
    }
}

public sealed record UnpublishKnowledgeArticleCommand(Guid ActorUserId, Guid ArticleId, int Version)
    : IRequest<KnowledgeArticleDetailDto>;

public sealed class UnpublishKnowledgeArticleCommandValidator : AbstractValidator<UnpublishKnowledgeArticleCommand>
{
    public UnpublishKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class UnpublishKnowledgeArticleCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<UnpublishKnowledgeArticleCommand, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(UnpublishKnowledgeArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await repository.FindArticleForUpdateAsync(request.ArticleId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
        if (article.Version != request.Version)
            throw new KnowledgeConflictException("Bài viết đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        if (article.Status != KnowledgeArticleStatus.Published)
            throw new KnowledgeConflictException("Chỉ có thể gỡ bài viết đang ở trạng thái xuất bản.");

        article.Unpublish(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return await repository.GetArticleDetailAsync(article.Id, cancellationToken)
               ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
    }
}

public sealed record ArchiveKnowledgeArticleCommand(Guid ActorUserId, Guid ArticleId, int Version)
    : IRequest<KnowledgeArticleDetailDto>;

public sealed class ArchiveKnowledgeArticleCommandValidator : AbstractValidator<ArchiveKnowledgeArticleCommand>
{
    public ArchiveKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class ArchiveKnowledgeArticleCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<ArchiveKnowledgeArticleCommand, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(ArchiveKnowledgeArticleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await repository.FindArticleForUpdateAsync(request.ArticleId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
        if (article.Version != request.Version)
            throw new KnowledgeConflictException("Bài viết đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        if (article.Status == KnowledgeArticleStatus.Archived)
            throw new KnowledgeConflictException("Bài viết đã ở trạng thái lưu trữ.");

        article.Archive(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return await repository.GetArticleDetailAsync(article.Id, cancellationToken)
               ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
    }
}

public sealed record DeleteKnowledgeArticleCommand(Guid ArticleId, int Version) : IRequest;

public sealed class DeleteKnowledgeArticleCommandValidator : AbstractValidator<DeleteKnowledgeArticleCommand>
{
    public DeleteKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class DeleteKnowledgeArticleCommandHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<DeleteKnowledgeArticleCommand>
{
    public async Task Handle(DeleteKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await repository.FindArticleForUpdateAsync(request.ArticleId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Không tìm thấy bài viết kiến thức.");
        if (article.Version != request.Version)
            throw new KnowledgeConflictException("Bài viết đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        if (!article.CanBeDeleted)
            throw new KnowledgeConflictException(
                "Không thể xóa trực tiếp bài viết đang xuất bản. Hãy gỡ bài viết về bản nháp hoặc lưu trữ trước khi xóa.");

        repository.RemoveArticle(article);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
