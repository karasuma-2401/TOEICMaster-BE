using FluentValidation;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams.Commands.CreateScoreProfile;

public sealed record ScoreConversionInput(ExamSkill Skill, int RawCorrectCount, int ScaledScore);

public sealed record CreateScoreProfileCommand(string Name, IReadOnlyList<ScoreConversionInput> Conversions)
    : IRequest<ScoreProfileDto>;

public sealed class CreateScoreProfileCommandValidator : AbstractValidator<CreateScoreProfileCommand>
{
    public CreateScoreProfileCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleForEach(command => command.Conversions).ChildRules(conversion =>
        {
            conversion.RuleFor(item => item.RawCorrectCount).InclusiveBetween(0, 100);
            conversion.RuleFor(item => item.ScaledScore).InclusiveBetween(5, 495).Must(score => score % 5 == 0);
        });
        RuleFor(command => command.Conversions).Must(conversions => conversions
            .GroupBy(item => new { item.Skill, item.RawCorrectCount }).All(group => group.Count() == 1))
            .WithMessage("Mỗi skill chỉ có một mốc cho mỗi số câu đúng.");
    }
}

public sealed class CreateScoreProfileCommandHandler(IExamRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateScoreProfileCommand, ScoreProfileDto>
{
    public async Task<ScoreProfileDto> Handle(CreateScoreProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = ScoreProfile.Create(request.Name, timeProvider.GetUtcNow());
        foreach (var conversion in request.Conversions)
            profile.SetScore(conversion.Skill, conversion.RawCorrectCount, conversion.ScaledScore);
        await repository.AddScoreProfileAsync(profile, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ScoreProfileDto.From(profile);
    }
}
