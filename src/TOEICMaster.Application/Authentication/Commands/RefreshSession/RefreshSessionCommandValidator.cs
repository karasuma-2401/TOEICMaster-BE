using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.RefreshSession;

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(command => command.RefreshToken).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Refresh token is required.")
            .MaximumLength(512).WithMessage("Invalid refresh token.");
    }
}
