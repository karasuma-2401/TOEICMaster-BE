using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.VerifyPasswordReset;

public sealed class VerifyPasswordResetCommandValidator : AbstractValidator<VerifyPasswordResetCommand>
{
    public VerifyPasswordResetCommandValidator()
    {
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .Must(email => email.Trim().Length <= 254 && new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Invalid email.");
        RuleFor(command => command.Code).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches("^[0-9]{6}$").WithMessage("Verification code must be 6 digits.");
    }
}
