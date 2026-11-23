using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .Must(email => email.Trim().Length <= 254).WithMessage("Invalid email.")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim())).WithMessage("Invalid email.");
        RuleFor(command => command.Password).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Invalid password.");
    }
}
