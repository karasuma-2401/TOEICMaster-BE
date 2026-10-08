using MediatR;

namespace TOEICMaster.Application.Email.Commands.SendEmail;

public sealed record SendEmailCommand(string To, string Subject, string TextBody) : IRequest<Unit>;

public sealed class SendEmailCommandHandler(IMailSender mailSender) : IRequestHandler<SendEmailCommand, Unit>
{
    public async Task<Unit> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        await mailSender.SendAsync(new MailMessage(request.To, request.Subject, request.TextBody), cancellationToken);
        return Unit.Value;
    }
}
