using FluentValidation;

namespace TOEICMaster.Application.Authentication.Commands.LoginWithGoogle;

public sealed class LoginWithGoogleCommandValidator : AbstractValidator<LoginWithGoogleCommand>
{
    public LoginWithGoogleCommandValidator()
    {
        RuleFor(command => command.Credential).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Google credential là bắt buộc.")
            .MaximumLength(8192).WithMessage("Google credential không hợp lệ.");
    }
}
