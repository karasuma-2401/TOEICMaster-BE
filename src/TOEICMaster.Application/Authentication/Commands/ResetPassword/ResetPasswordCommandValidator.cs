using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.ResetToken)
            .NotEmpty().WithMessage("Reset token is required.")
            .MaximumLength(256).WithMessage("Invalid reset token.");
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be between 8 and 128 characters.")
            .MaximumLength(128).WithMessage("New password must be between 8 and 128 characters.");
    }
}
