using FluentValidation;

namespace TOEICMaster.Application.Email.Commands.SendEmail;

public sealed class SendEmailCommandValidator : AbstractValidator<SendEmailCommand>
{
    public SendEmailCommandValidator()
    {
        RuleFor(request => request.To).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(request => request.Subject).NotEmpty().MaximumLength(200);
        RuleFor(request => request.TextBody).NotEmpty().MaximumLength(100_000);
    }
}
