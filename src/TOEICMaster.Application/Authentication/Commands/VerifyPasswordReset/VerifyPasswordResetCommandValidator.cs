using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.VerifyPasswordReset;

public sealed class VerifyPasswordResetCommandValidator : AbstractValidator<VerifyPasswordResetCommand>
{
    public VerifyPasswordResetCommandValidator()
    {
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .Must(email => email.Trim().Length <= 254 && new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Email không hợp lệ.");
        RuleFor(command => command.Code).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mã xác minh là bắt buộc.")
            .Matches("^[0-9]{6}$").WithMessage("Mã xác minh phải gồm 6 chữ số.");
    }
}
