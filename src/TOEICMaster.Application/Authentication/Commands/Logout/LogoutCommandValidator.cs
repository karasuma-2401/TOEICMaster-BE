using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.Logout;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(command => command.RefreshToken).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Refresh token là bắt buộc.")
            .MaximumLength(512).WithMessage("Refresh token không hợp lệ.");
    }
}
