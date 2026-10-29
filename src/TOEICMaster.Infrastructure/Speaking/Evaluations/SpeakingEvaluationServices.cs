using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Infrastructure.Persistence.Speaking;
using TOEICMaster.Infrastructure.Writing.Evaluations;

namespace TOEICMaster.Infrastructure.Speaking.Evaluations;

public static class SpeakingEvaluationServices
{
    /// <summary>
    /// AI evaluation for Speaking: Azure Speech (speech to text + pronunciation assessment) and the Gemini key
    /// already configured for Writing (grammar, vocabulary, relevance).
    /// </summary>
    public static IServiceCollection AddSpeakingEvaluation(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AzureSpeechOptions>().Bind(configuration.GetSection(AzureSpeechOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Key),
                "Azure Speech key is required when Azure Speech is enabled.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Region) ||
                                 Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _),
                "Azure Speech region or endpoint is required when Azure Speech is enabled.")
            .ValidateOnStart();
        services.AddSingleton<ISpeakingAudioConverter, FfmpegSpeakingAudioConverter>();
        services.AddHttpClient<ISpeakingSpeechAssessor, AzureSpeakingSpeechAssessor>();
        services.AddHttpClient<ISpeakingLanguageEvaluator, GeminiSpeakingLanguageEvaluator>(client =>
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
        services.AddScoped<ISpeakingEvaluationSettings, SpeakingEvaluationSettings>();
        services.AddScoped<ISpeakingEvaluationRepository, EfSpeakingEvaluationRepository>();
        services.AddScoped<ISpeakingEvaluationProcessor, SpeakingEvaluationProcessor>();
        services.AddHostedService<SpeakingEvaluationWorker>();
        return services;
    }
}

public sealed class SpeakingEvaluationSettings(IOptions<AzureSpeechOptions> speech, IOptions<GeminiOptions> gemini)
    : ISpeakingEvaluationSettings
{
    public const string Rubric = "toeic-speaking-v1";

    public bool IsConfigured => speech.Value.Enabled && !string.IsNullOrWhiteSpace(speech.Value.Key) &&
                                gemini.Value.Enabled && !string.IsNullOrWhiteSpace(gemini.Value.ApiKey);
    public string SpeechProvider => "azure-speech";
    public string SpeechLocale => speech.Value.Locale;
    public string LanguageProvider => "gemini";
    public string LanguageModel => gemini.Value.Model;
    public string RubricVersion => Rubric;
}

public sealed class SpeakingEvaluationWorker(IServiceScopeFactory scopeFactory,
    ILogger<SpeakingEvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ISpeakingEvaluationProcessor>();
                var processed = await processor.ProcessNextAsync(stoppingToken);
                await Task.Delay(processed ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Speaking evaluation worker could not process a job.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
