using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.ResetToken)
            .NotEmpty().WithMessage("Reset token là bắt buộc.")
            .MaximumLength(256).WithMessage("Reset token không hợp lệ.");
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới là bắt buộc.")
            .MinimumLength(8).WithMessage("Mật khẩu mới cần từ 8 đến 128 ký tự.")
            .MaximumLength(128).WithMessage("Mật khẩu mới cần từ 8 đến 128 ký tự.");
    }
}
