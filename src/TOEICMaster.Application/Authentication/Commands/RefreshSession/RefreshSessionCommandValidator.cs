using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.RefreshSession;

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(command => command.RefreshToken).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Refresh token là bắt buộc.")
            .MaximumLength(512).WithMessage("Refresh token không hợp lệ.");
    }
}
