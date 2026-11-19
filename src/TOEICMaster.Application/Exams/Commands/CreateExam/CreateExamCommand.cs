using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.CreateExam;

public sealed record CreateExamCommand(string Code, string Title, Guid ScoreProfileId) : IRequest<ExamDraftDto>;

public sealed class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9][A-Za-z0-9_-]*$");
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ScoreProfileId).NotEqual(Guid.Empty);
    }
}

public sealed class CreateExamCommandHandler(IExamRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateExamCommand, ExamDraftDto>
{
    public async Task<ExamDraftDto> Handle(CreateExamCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        if (await repository.ExamCodeExistsAsync(code, cancellationToken))
            throw new ExamConflictException();
        if (await repository.FindScoreProfileAsync(request.ScoreProfileId, cancellationToken) is null)
            throw new ExamNotFoundException();

        var exam = Exam.Create(code, request.Title, request.ScoreProfileId, timeProvider.GetUtcNow());
        var revision = exam.Revisions.Single();
        await repository.AddExamAsync(exam, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ExamDraftDto.From(exam, revision);
    }
}
