using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.UpdateExamQuestion;

public sealed record UpdateExamQuestionCommand(Guid ExamId, Guid RevisionId, int QuestionNumber,
    ExamQuestionContent Content) : IRequest;

public sealed class UpdateExamQuestionCommandValidator : AbstractValidator<UpdateExamQuestionCommand>
{
    public UpdateExamQuestionCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEqual(Guid.Empty);
        RuleFor(command => command.RevisionId).NotEqual(Guid.Empty);
        RuleFor(command => command.QuestionNumber).InclusiveBetween(1, 200);
        RuleFor(command => command.Content).NotNull();
        When(command => command.Content is not null, () =>
        {
            RuleFor(command => command.Content.Code).NotEmpty().MaximumLength(80);
            RuleFor(command => command.Content.Stem).NotEmpty().MaximumLength(10000);
            RuleFor(command => command.Content.OptionA).NotEmpty().MaximumLength(5000);
            RuleFor(command => command.Content.OptionB).NotEmpty().MaximumLength(5000);
            RuleFor(command => command.Content.OptionC).NotEmpty().MaximumLength(5000);
        });
    }
}

public sealed class UpdateExamQuestionCommandHandler(IExamRepository repository)
    : IRequestHandler<UpdateExamQuestionCommand>
{
    public async Task Handle(UpdateExamQuestionCommand request, CancellationToken cancellationToken)
    {
        var exam = await repository.FindExamAsync(request.ExamId, cancellationToken) ?? throw new ExamNotFoundException();
        if (exam.Revisions.Any(revision => revision.Id == request.RevisionId) is false)
            throw new ExamNotFoundException();
        if (request.Content.ImageAssetId is { } imageAssetId && !await repository.ImageAssetsExistAsync([imageAssetId],
                cancellationToken))
            throw new InvalidExamMediaException();
        exam.Revisions.Single(revision => revision.Id == request.RevisionId)
            .UpdateQuestion(request.QuestionNumber, request.Content);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
