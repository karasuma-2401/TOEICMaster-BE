using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .Must(email => email.Trim().Length <= 254 && new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Email không hợp lệ.");
    }
}
