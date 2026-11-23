using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Knowledge.Admin;

public sealed record GetAdminKnowledgeCategoriesQuery(KnowledgeAdminCategoryFilter Filter)
    : IRequest<KnowledgeCategoryPageDto>;

public sealed class GetAdminKnowledgeCategoriesQueryValidator : AbstractValidator<GetAdminKnowledgeCategoriesQuery>
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "displayOrder:asc", "displayOrder:desc", "name:asc", "name:desc",
        "updatedAt:desc", "updatedAt:asc", "createdAt:desc", "createdAt:asc"
    };

    public GetAdminKnowledgeCategoriesQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageNumber).Cascade(CascadeMode.Stop).GreaterThanOrEqualTo(1)
                .Must((query, pageNumber) => KnowledgePagination.CanRepresentOffset(pageNumber, query.Filter.PageSize))
                .WithMessage("Page number exceeds the limit for the selected page size.");
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Filter.Query).MaximumLength(100);
            RuleFor(x => x.Filter.Sort)
                .Must(sort => string.IsNullOrWhiteSpace(sort) || AllowedSorts.Contains(sort.Trim()))
                .WithMessage("Invalid category sort option.");
        });
    }
}

public sealed class GetAdminKnowledgeCategoriesQueryHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<GetAdminKnowledgeCategoriesQuery, KnowledgeCategoryPageDto>
{
    public Task<KnowledgeCategoryPageDto> Handle(GetAdminKnowledgeCategoriesQuery request,
        CancellationToken cancellationToken) =>
        repository.GetCategoriesAsync(request.Filter, cancellationToken);
}

public sealed record CreateKnowledgeCategoryCommand(Guid ActorUserId, KnowledgeCategoryInput Input)
    : IRequest<KnowledgeCategoryDto>;

public sealed class CreateKnowledgeCategoryCommandValidator : AbstractValidator<CreateKnowledgeCategoryCommand>
{
    public CreateKnowledgeCategoryCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Input.Slug).MaximumLength(120)
                .Must(slug => string.IsNullOrWhiteSpace(slug) ||
                              KnowledgeSlug.IsValid(slug.Trim().ToLowerInvariant(), 120))
                .WithMessage("Slug may only contain lowercase unaccented letters, numbers, and hyphens.");
            RuleFor(x => x.Input.Description).MaximumLength(500);
            RuleFor(x => x.Input.DisplayOrder).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateKnowledgeCategoryCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<CreateKnowledgeCategoryCommand, KnowledgeCategoryDto>
{
    public async Task<KnowledgeCategoryDto> Handle(CreateKnowledgeCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var slug = KnowledgeSlug.Resolve(request.Input.Slug, request.Input.Name, 120);
        if (!KnowledgeSlug.IsValid(slug, 120))
            throw new InvalidKnowledgeContentException("Could not generate a valid slug from the category name. Please enter a slug manually.", "slug");

        if (await repository.CategorySlugExistsAsync(slug, null, cancellationToken))
            throw new KnowledgeConflictException($"Category slug '{slug}' already exists.");

        var now = timeProvider.GetUtcNow();
        var category = KnowledgeCategory.Create(request.Input.Name, slug, request.Input.Description,
            request.Input.DisplayOrder, request.ActorUserId, now);
        await repository.AddCategoryAsync(category, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new KnowledgeCategoryDto(category.Id, category.Name, category.Slug, category.Description,
            category.DisplayOrder, 0, category.CreatedAtUtc, category.UpdatedAtUtc, category.Version);
    }
}

public sealed record UpdateKnowledgeCategoryCommand(
    Guid ActorUserId,
    Guid CategoryId,
    int Version,
    KnowledgeCategoryInput Input) : IRequest<KnowledgeCategoryDto>;

public sealed class UpdateKnowledgeCategoryCommandValidator : AbstractValidator<UpdateKnowledgeCategoryCommand>
{
    public UpdateKnowledgeCategoryCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Input).NotNull();
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Input.Slug).MaximumLength(120)
                .Must(slug => string.IsNullOrWhiteSpace(slug) ||
                              KnowledgeSlug.IsValid(slug.Trim().ToLowerInvariant(), 120))
                .WithMessage("Slug may only contain lowercase unaccented letters, numbers, and hyphens.");
            RuleFor(x => x.Input.Description).MaximumLength(500);
            RuleFor(x => x.Input.DisplayOrder).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateKnowledgeCategoryCommandHandler(
    IKnowledgeAdminRepository repository,
    TimeProvider timeProvider) : IRequestHandler<UpdateKnowledgeCategoryCommand, KnowledgeCategoryDto>
{
    public async Task<KnowledgeCategoryDto> Handle(UpdateKnowledgeCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await repository.FindCategoryForUpdateAsync(request.CategoryId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Knowledge category not found.");
        if (category.Version != request.Version)
            throw new KnowledgeConflictException("Category was updated by another user. Please reload the data.");

        var slug = KnowledgeSlug.Resolve(request.Input.Slug, request.Input.Name, 120);
        if (!KnowledgeSlug.IsValid(slug, 120))
            throw new InvalidKnowledgeContentException("Could not generate a valid slug from the category name. Please enter a slug manually.", "slug");

        if (await repository.CategorySlugExistsAsync(slug, category.Id, cancellationToken))
            throw new KnowledgeConflictException($"Category slug '{slug}' already exists.");

        category.Update(request.Input.Name, slug, request.Input.Description, request.Input.DisplayOrder,
            request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        var articleCount = await repository.GetCategoryArticleCountAsync(category.Id, cancellationToken);
        return new KnowledgeCategoryDto(category.Id, category.Name, category.Slug, category.Description,
            category.DisplayOrder, articleCount, category.CreatedAtUtc, category.UpdatedAtUtc, category.Version);
    }
}

public sealed record DeleteKnowledgeCategoryCommand(Guid CategoryId, int Version) : IRequest;

public sealed class DeleteKnowledgeCategoryCommandValidator : AbstractValidator<DeleteKnowledgeCategoryCommand>
{
    public DeleteKnowledgeCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class DeleteKnowledgeCategoryCommandHandler(IKnowledgeAdminRepository repository)
    : IRequestHandler<DeleteKnowledgeCategoryCommand>
{
    public async Task Handle(DeleteKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await repository.FindCategoryForUpdateAsync(request.CategoryId, cancellationToken)
            ?? throw new KnowledgeNotFoundException("Knowledge category not found.");
        if (category.Version != request.Version)
            throw new KnowledgeConflictException("Category was updated by another user. Please reload the data.");

        if (await repository.CategoryHasArticlesAsync(category.Id, cancellationToken))
            throw new KnowledgeConflictException(
                "Cannot delete a category that still contains articles. Move the articles to another category first.");

        repository.RemoveCategory(category);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
