using FluentValidation;
using MediatR;
using TOEICMaster.Application.Authentication;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Accounts.Commands.ResetAdminUserPassword;

public sealed record ResetAdminUserPasswordCommand(Guid ActorUserId, Guid TargetUserId) : IRequest;

public sealed class ResetAdminUserPasswordCommandValidator : AbstractValidator<ResetAdminUserPasswordCommand>
{
    public ResetAdminUserPasswordCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TargetUserId).NotEmpty();
    }
}

public sealed class ResetAdminUserPasswordCommandHandler(
    IAdminUserRepository repository,
    IPasswordResetRepository passwordResetRepository,
    IEmailVerificationCodeService codeService,
    IMailSender mailSender,
    TimeProvider timeProvider) : IRequestHandler<ResetAdminUserPasswordCommand>
{
    public async Task Handle(ResetAdminUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var target = await repository.GetPasswordResetTargetAsync(request.TargetUserId, cancellationToken)
            ?? throw new AdminUserNotFoundException();

        if (!target.HasPassword)
            throw new AdminUserOperationConflictException("PASSWORD_RESET_UNSUPPORTED_PROVIDER",
                "Google sign-in accounts do not support password reset.");

        if (!target.IsActive || !target.EmailVerified)
            throw new AdminUserOperationConflictException("USER_INACTIVE_OR_UNVERIFIED",
                "Password reset can only be sent to active accounts with a verified email.");

        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(target.NormalizedEmail);

        if (!await passwordResetRepository.TryReplaceChallengeAsync(target.UserId, code.CodeHash, nowUtc,
                nowUtc + codeService.Lifetime, codeService.ResendCooldown, cancellationToken))
        {
            throw new AdminUserRateLimitException("PASSWORD_RESET_COOLDOWN",
                "Please wait before resending the password reset code.");
        }

        try
        {
            await mailSender.SendAsync(PasswordResetMail.Create(target.Email, code.Code, codeService.Lifetime),
                cancellationToken);
        }
        catch
        {
            try
            {
                await passwordResetRepository.ReleaseChallengeCooldownAsync(target.UserId, nowUtc,
                    codeService.ResendCooldown, CancellationToken.None);
            }
            catch
            {
                // Preserve the mail delivery exception; cooldown release is a best-effort retry aid.
            }

            throw;
        }
    }
}
