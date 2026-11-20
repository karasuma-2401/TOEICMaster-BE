using TOEICMaster.Application.Pronunciation;

namespace TOEICMaster.Tests.TestDoubles;

/// <summary>Assessor that hears nothing; for containers that only need every handler to resolve.</summary>
public sealed class SilentPronunciationAssessor : IPronunciationAssessor
{
    public Task<PronunciationAssessmentDto> AssessAsync(byte[] audio, string contentType, string referenceText,
        string locale, CancellationToken cancellationToken) =>
        Task.FromResult(PronunciationAssessmentDto.NoSpeech(referenceText));
}
