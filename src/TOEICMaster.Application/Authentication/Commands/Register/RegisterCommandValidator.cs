using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.DisplayName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Tên hiển thị là bắt buộc.")
            .Must(displayName => displayName.Trim().Length <= 100)
            .WithMessage("Tên hiển thị không được vượt quá 100 ký tự.");
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .Must(email => email.Trim().Length <= 254).WithMessage("Email không hợp lệ.")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim())).WithMessage("Email không hợp lệ.");
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc.")
            .MinimumLength(8).WithMessage("Mật khẩu cần từ 8 đến 128 ký tự.")
            .MaximumLength(128).WithMessage("Mật khẩu cần từ 8 đến 128 ký tự.");
    }
}
