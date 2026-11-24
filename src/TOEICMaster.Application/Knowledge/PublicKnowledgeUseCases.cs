using FluentValidation;
using MediatR;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Application.Knowledge;

public sealed record GetPublicKnowledgeArticlesQuery(KnowledgeAdminArticleFilter Filter) : IRequest<KnowledgeArticlePageDto>;
public sealed class GetPublicKnowledgeArticlesQueryHandler(ISender sender)
    : IRequestHandler<GetPublicKnowledgeArticlesQuery, KnowledgeArticlePageDto>
{
    public async Task<KnowledgeArticlePageDto> Handle(GetPublicKnowledgeArticlesQuery request, CancellationToken ct)
    {
        var page = await sender.Send(new GetAdminKnowledgeArticlesQuery(request.Filter with
        { Status = "Published", Sort = request.Filter.Sort ?? "publishedAt:desc" }), ct);
        return page with { Items = page.Items.Select(PublicKnowledgeMedia.Summary).ToArray() };
    }
}

public sealed record GetPublicKnowledgeCategoriesQuery : IRequest<IReadOnlyList<KnowledgeCategoryDto>>;
public sealed class GetPublicKnowledgeCategoriesQueryHandler(IKnowledgeReader reader)
    : IRequestHandler<GetPublicKnowledgeCategoriesQuery, IReadOnlyList<KnowledgeCategoryDto>>
{
    public Task<IReadOnlyList<KnowledgeCategoryDto>> Handle(GetPublicKnowledgeCategoriesQuery request, CancellationToken ct) =>
        reader.CategoriesAsync(ct);
}

public sealed record GetPublicKnowledgeArticleQuery(string Slug) : IRequest<KnowledgeArticleDetailDto>;
public sealed class GetPublicKnowledgeArticleQueryHandler(IKnowledgeReader reader, ISender sender)
    : IRequestHandler<GetPublicKnowledgeArticleQuery, KnowledgeArticleDetailDto>
{
    public async Task<KnowledgeArticleDetailDto> Handle(GetPublicKnowledgeArticleQuery request, CancellationToken ct)
    {
        var id = await reader.FindPublishedAsync(request.Slug, ct) ?? throw new KnowledgeNotFoundException("Lesson not found.");
        var article = await sender.Send(new GetAdminKnowledgeArticleQuery(id), ct);
        if (article.Status != "Published") throw new KnowledgeNotFoundException("Lesson not found.");
        return article with { CoverImageUrl = PublicKnowledgeMedia.Url(article.CoverImageAssetId) };
    }
}

public sealed record GetRelatedKnowledgeArticlesQuery(string Slug) : IRequest<IReadOnlyList<KnowledgeArticleSummaryDto>>;
public sealed class GetRelatedKnowledgeArticlesQueryHandler(IKnowledgeReader reader)
    : IRequestHandler<GetRelatedKnowledgeArticlesQuery, IReadOnlyList<KnowledgeArticleSummaryDto>>
{
    public async Task<IReadOnlyList<KnowledgeArticleSummaryDto>> Handle(GetRelatedKnowledgeArticlesQuery request, CancellationToken ct)
    {
        var id = await reader.FindPublishedAsync(request.Slug, ct) ?? throw new KnowledgeNotFoundException("Lesson not found.");
        return (await reader.RelatedAsync(id, ct)).Select(PublicKnowledgeMedia.Summary).ToArray();
    }
}

public sealed record GetPublicKnowledgeImageQuery(Guid AssetId) : IRequest<KnowledgeMediaBinary>;
public sealed class GetPublicKnowledgeImageQueryHandler(IKnowledgeReader reader, ISender sender)
    : IRequestHandler<GetPublicKnowledgeImageQuery, KnowledgeMediaBinary>
{
    public async Task<KnowledgeMediaBinary> Handle(GetPublicKnowledgeImageQuery request, CancellationToken ct)
    {
        if (!await reader.IsPublicMediaAsync(request.AssetId, ct)) throw new KnowledgeNotFoundException("Image not found.");
        return await sender.Send(new GetAdminKnowledgeImageQuery(request.AssetId), ct);
    }
}

public sealed record GetSavedKnowledgeArticlesQuery(Guid UserId, int PageNumber = 1, int PageSize = 12,
    Guid? ArticleId = null) : IRequest<SavedKnowledgeArticlePage>;
public sealed class GetSavedKnowledgeArticlesQueryValidator : AbstractValidator<GetSavedKnowledgeArticlesQuery>
{
    public GetSavedKnowledgeArticlesQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1)
            .Must((query, page) => KnowledgePagination.CanRepresentOffset(page, query.PageSize))
            .WithMessage("Page number exceeds the limit for the selected page size.");
        RuleFor(x => x.ArticleId).Must(id => id is null || id != Guid.Empty);
    }
}
public sealed class GetSavedKnowledgeArticlesQueryHandler(IKnowledgeReader reader)
    : IRequestHandler<GetSavedKnowledgeArticlesQuery, SavedKnowledgeArticlePage>
{
    public Task<SavedKnowledgeArticlePage> Handle(GetSavedKnowledgeArticlesQuery request, CancellationToken ct) =>
        reader.SavedAsync(request.UserId, request.PageNumber, request.PageSize, request.ArticleId, ct);
}

public sealed record SaveKnowledgeArticleCommand(Guid UserId, Guid ArticleId) : IRequest;
public sealed class SaveKnowledgeArticleCommandValidator : AbstractValidator<SaveKnowledgeArticleCommand>
{
    public SaveKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ArticleId).NotEmpty();
    }
}
public sealed class SaveKnowledgeArticleCommandHandler(IKnowledgeReader reader) : IRequestHandler<SaveKnowledgeArticleCommand>
{
    public Task Handle(SaveKnowledgeArticleCommand request, CancellationToken ct) => reader.SaveAsync(request.UserId, request.ArticleId, ct);
}

public sealed record RemoveSavedKnowledgeArticleCommand(Guid UserId, Guid ArticleId) : IRequest;
public sealed class RemoveSavedKnowledgeArticleCommandValidator : AbstractValidator<RemoveSavedKnowledgeArticleCommand>
{
    public RemoveSavedKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ArticleId).NotEmpty();
    }
}
public sealed class RemoveSavedKnowledgeArticleCommandHandler(IKnowledgeReader reader) : IRequestHandler<RemoveSavedKnowledgeArticleCommand>
{
    public Task Handle(RemoveSavedKnowledgeArticleCommand request, CancellationToken ct) => reader.RemoveAsync(request.UserId, request.ArticleId, ct);
}

internal static class PublicKnowledgeMedia
{
    public static string? Url(Guid? assetId) => assetId is null ? null : $"/api/knowledge/media/{assetId}";
    public static KnowledgeArticleSummaryDto Summary(KnowledgeArticleSummaryDto article) =>
        article with { CoverImageUrl = Url(article.CoverImageAssetId) };
}
