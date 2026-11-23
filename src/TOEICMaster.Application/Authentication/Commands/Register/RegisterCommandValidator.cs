using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.DisplayName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Display name is required.")
            .Must(displayName => displayName.Trim().Length <= 100)
            .WithMessage("Display name must not exceed 100 characters.");
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .Must(email => email.Trim().Length <= 254).WithMessage("Invalid email.")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim())).WithMessage("Invalid email.");
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be between 8 and 128 characters.")
            .MaximumLength(128).WithMessage("Password must be between 8 and 128 characters.");
    }
}
