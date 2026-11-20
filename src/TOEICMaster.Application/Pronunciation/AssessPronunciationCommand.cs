using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Pronunciation;

public sealed record AssessPronunciationCommand(byte[] Audio, string ReferenceText, string? Locale)
    : IRequest<PronunciationAssessmentDto>;

public static class PronunciationRules
{
    public const int MaxAudioBytes = 1024 * 1024;
    public const double MaxAudioSeconds = 10;
    public const int MaxReferenceTextLength = 40;
    public const string DefaultLocale = "en-US";
    public static readonly string[] Locales = ["en-US", "en-GB"];

    /// <summary>English letters, spaces, apostrophes and hyphens only.</summary>
    public static bool IsReferenceText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaxReferenceTextLength &&
        value.Trim().All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or ' ' or '\'' or '-');

    /// <summary>Audio container from the file signature; the declared Content-Type is not trusted.</summary>
    public static string? DetectAudio(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 4 && bytes.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }))
            return "audio/webm";
        if (bytes.Length >= 4 && bytes.StartsWith("OggS"u8))
            return "audio/ogg";
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
            return "audio/mp4";
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
            return "audio/wav";
        return null;
    }
}

public sealed class AssessPronunciationCommandValidator : AbstractValidator<AssessPronunciationCommand>
{
    public AssessPronunciationCommandValidator()
    {
        RuleFor(command => command.Audio).NotEmpty().WithMessage("Thiếu bản thu.")
            .Must(audio => audio.Length <= PronunciationRules.MaxAudioBytes)
            .WithMessage("Bản thu không được vượt quá 1 MiB.");
        RuleFor(command => command.ReferenceText).Must(PronunciationRules.IsReferenceText)
            .WithMessage("Từ luyện tập chỉ gồm 1–40 chữ cái tiếng Anh, khoảng trắng, dấu ' hoặc -.");
        RuleFor(command => command.Locale)
            .Must(locale => locale is null || PronunciationRules.Locales.Contains(locale))
            .WithMessage("Giọng chấm chỉ hỗ trợ en-US hoặc en-GB.");
    }
}

public sealed class AssessPronunciationCommandHandler(IPronunciationAssessor assessor)
    : IRequestHandler<AssessPronunciationCommand, PronunciationAssessmentDto>
{
    public Task<PronunciationAssessmentDto> Handle(AssessPronunciationCommand request,
        CancellationToken cancellationToken)
    {
        var contentType = PronunciationRules.DetectAudio(request.Audio) ??
                          throw PronunciationAssessmentException.InvalidAudio(
                              "Định dạng bản thu không được hỗ trợ (WebM, Ogg, MP4 hoặc WAV).");
        return assessor.AssessAsync(request.Audio, contentType, request.ReferenceText.Trim(),
            request.Locale ?? PronunciationRules.DefaultLocale, cancellationToken);
    }
}
