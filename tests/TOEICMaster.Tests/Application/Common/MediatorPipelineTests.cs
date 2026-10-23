using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Application.Email.Commands.SendEmail;
using TOEICMaster.Application.Profile;
using TOEICMaster.Application.System.Queries.GetSystemStatus;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Common;

public sealed class MediatorPipelineTests
{
    private static ServiceProvider CreateServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IAccountReader, SampleAccountReader>();
        services.AddSingleton<IProfileRepository, EmptyProfileRepository>();
        services.AddSingleton<IAvatarStore, EmptyAvatarStore>();
        services.AddSingleton<IWritingAdminRepository, EmptyWritingAdminRepository>();
        services.AddSingleton<IWritingAttemptRepository, EmptyWritingAttemptRepository>();
        services.AddSingleton<IWritingEvaluationRepository, EmptyWritingEvaluationRepository>();
        services.AddSingleton<IWritingEvaluationSettings, StaticWritingEvaluationSettings>();
        services.AddSingleton<IWritingImageStore, EmptyWritingImageStore>();
        services.AddSingleton<IWritingExerciseImportParser, EmptyWritingExerciseImportParser>();
        services.AddSingleton<RecordingMailSender>();
        services.AddSingleton<IMailSender>(provider => provider.GetRequiredService<RecordingMailSender>());
        services.AddSingleton<IUserRegistrationRepository, FakeUserRegistrationRepository>();
        services.AddSingleton<IAuthenticationRepository, FakeAuthenticationRepository>();
        services.AddSingleton<IExternalLoginRepository, FakeExternalLoginRepository>();
        services.AddSingleton<IPasswordResetRepository, FakePasswordResetRepository>();
        services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
        services.AddSingleton<IAuthenticationTokenService, FakeAuthenticationTokenService>();
        services.AddSingleton<IGoogleIdentityVerifier, FakeGoogleIdentityVerifier>();
        services.AddSingleton<IEmailVerificationCodeService, FakeEmailVerificationCodeService>();
        services.AddSingleton<IPasswordResetTokenService, FakePasswordResetTokenService>();
        services.AddSingleton(TimeProvider.System);
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task Valid_command_reaches_the_mail_adapter_and_preserves_cancellation_token()
    {
        using var provider = CreateServices();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var command = new SendEmailCommand("learner@toeicmaster.test", "Practice reminder", "Time to practice.");
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellation.Token);

        var mail = provider.GetRequiredService<RecordingMailSender>();
        Assert.Equal(new MailMessage(command.To, command.Subject, command.TextBody), Assert.Single(mail.Messages));
        Assert.Equal(cancellation.Token, mail.LastCancellationToken);
    }

    [Theory]
    [InlineData("invalid", "Practice reminder", "Time to practice.", "To")]
    [InlineData("learner@toeicmaster.test", " ", "Time to practice.", "Subject")]
    [InlineData("learner@toeicmaster.test", "Practice reminder", "", "TextBody")]
    public async Task Invalid_command_is_rejected_before_any_side_effect(string to, string subject, string body, string property)
    {
        using var provider = CreateServices();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var exception = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new SendEmailCommand(to, subject, body)));

        Assert.Contains(exception.Errors, failure => failure.PropertyName == property);
        Assert.Empty(provider.GetRequiredService<RecordingMailSender>().Messages);
    }

    [Fact]
    public async Task Asynchronous_validator_is_awaited_and_can_block_the_handler()
    {
        using var provider = CreateServices(services => services.AddScoped<IValidator<SendEmailCommand>, AsyncMailValidator>());
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = new SendEmailCommand("learner@toeicmaster.test", "Practice reminder", "Time to practice.");
        var exception = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(command));

        Assert.Contains(exception.Errors, failure => failure.ErrorMessage == "Recipient blocked by async validation.");
        Assert.Empty(provider.GetRequiredService<RecordingMailSender>().Messages);
    }

    [Fact]
    public async Task Multiple_validators_report_each_failure_once()
    {
        using var provider = CreateServices(services =>
        {
            services.AddScoped<IValidator<SendEmailCommand>, AsyncMailValidator>();
            services.AddScoped<IValidator<SendEmailCommand>, AsyncMailValidator>();
        });
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = new SendEmailCommand("learner@toeicmaster.test", "Practice reminder", "Time to practice.");
        var exception = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(command));
        Assert.Equal(2, exception.Errors.Count());
        Assert.Empty(provider.GetRequiredService<RecordingMailSender>().Messages);
    }

    [Fact]
    public async Task Cancelled_command_does_not_send_mail()
    {
        using var provider = CreateServices();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = new SendEmailCommand("learner@toeicmaster.test", "Practice reminder", "Time to practice.");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.Send(command, new CancellationToken(true)));
        Assert.Empty(provider.GetRequiredService<RecordingMailSender>().Messages);
    }

    [Fact]
    public async Task Cancellation_also_applies_to_queries_without_validators()
    {
        using var provider = CreateServices();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.Send(new GetSystemStatusQuery(), new CancellationToken(true)));
    }

    private sealed class AsyncMailValidator : AbstractValidator<SendEmailCommand>
    {
        public AsyncMailValidator()
        {
            RuleFor(command => command.To).MustAsync(async (_, cancellationToken) =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }).WithMessage("Recipient blocked by async validation.");
        }
    }

    private sealed class EmptyProfileRepository : IProfileRepository
    {
        public Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> UpdateAvatarAsync(Guid userId, string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> ChangePasswordAsync(Guid userId, string currentHash, string newHash,
            DateTimeOffset changedAtUtc, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class EmptyAvatarStore : IAvatarStore
    {
        public Task UploadAsync(string objectKey, byte[] content, string contentType,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AvatarContent> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(new AvatarContent([], "image/png"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
