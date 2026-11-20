using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Pronunciation;

namespace TOEICMaster.Infrastructure.Pronunciation;

public static class PronunciationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the IPA pronunciation assessor. It uses the same Azure Speech resource as Speaking: settings come
    /// from the <c>AzureSpeech</c> section (<c>AzureSpeech__Key</c>, ...) or, when that section leaves a value
    /// empty, from the <c>AZURE_SPEECH_*</c> variables Docker Compose passes to Speaking. Missing settings do not
    /// stop the app: the endpoint then answers 503 NOT_CONFIGURED.
    /// </summary>
    public static IServiceCollection AddPronunciationAssessment(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PronunciationSpeechOptions>()
            .Bind(configuration.GetSection(PronunciationSpeechOptions.SectionName))
            .PostConfigure(options => ApplyEnvironmentFallback(options, configuration))
            .ValidateDataAnnotations();
        services.AddSingleton<IPronunciationAudioConverter, FfmpegPronunciationAudioConverter>();
        services.AddHttpClient<IPronunciationAssessor, AzurePronunciationAssessor>();
        return services;
    }

    internal static void ApplyEnvironmentFallback(PronunciationSpeechOptions options, IConfiguration configuration)
    {
        if (!options.Enabled && bool.TryParse(configuration["AZURE_SPEECH_ENABLED"], out var enabled))
            options.Enabled = enabled;
        if (string.IsNullOrWhiteSpace(options.Key))
            options.Key = configuration["AZURE_SPEECH_KEY"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.Region))
            options.Region = configuration["AZURE_SPEECH_REGION"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            options.Endpoint = configuration["AZURE_SPEECH_ENDPOINT"]?.Trim() is { Length: > 0 } endpoint
                ? endpoint
                : null;
    }
}
