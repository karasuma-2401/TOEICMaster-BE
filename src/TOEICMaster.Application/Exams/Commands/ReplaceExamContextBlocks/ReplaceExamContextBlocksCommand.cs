using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.ReplaceExamContextBlocks;

public sealed record ReplaceExamContextBlocksCommand(Guid ExamId, Guid RevisionId, string BlockKey,
    IReadOnlyList<ExamContextContent> Contexts) : IRequest;

public sealed class ReplaceExamContextBlocksCommandValidator : AbstractValidator<ReplaceExamContextBlocksCommand>
{
    public ReplaceExamContextBlocksCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEqual(Guid.Empty);
        RuleFor(command => command.RevisionId).NotEqual(Guid.Empty);
        RuleFor(command => command.BlockKey).NotEmpty().MaximumLength(32);
        RuleFor(command => command.Contexts).NotEmpty();
        RuleForEach(command => command.Contexts).ChildRules(context =>
        {
            context.RuleFor(item => item.Ordinal).GreaterThan(0);
            context.RuleFor(item => item.AltText).MaximumLength(500).When(item => item.AltText is not null);
        });
    }
}

public sealed class ReplaceExamContextBlocksCommandHandler(IExamRepository repository)
    : IRequestHandler<ReplaceExamContextBlocksCommand>
{
    public async Task Handle(ReplaceExamContextBlocksCommand request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        var revision = exam.Revisions.SingleOrDefault(candidate => candidate.Id == request.RevisionId)
            ?? throw new ExamNotFoundException();
        var imageAssetIds = request.Contexts.Where(context => context.ImageAssetId is not null)
            .Select(context => context.ImageAssetId!.Value).Distinct().ToArray();
        if (!await repository.ImageAssetsExistAsync(imageAssetIds, cancellationToken))
            throw new InvalidExamMediaException();
        revision.ReplaceContextBlocks(request.BlockKey, request.Contexts);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
