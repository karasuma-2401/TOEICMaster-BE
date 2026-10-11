using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .Must(email => email.Trim().Length <= 254).WithMessage("Email không hợp lệ.")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim())).WithMessage("Email không hợp lệ.");
        RuleFor(command => command.Password).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc.")
            .MaximumLength(128).WithMessage("Mật khẩu không hợp lệ.");
    }
}
