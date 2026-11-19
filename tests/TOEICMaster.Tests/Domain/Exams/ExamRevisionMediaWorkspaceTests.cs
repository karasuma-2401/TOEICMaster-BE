using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.Exams;

public sealed class ExamRevisionMediaWorkspaceTests
{
    [Fact]
    public void Revision_requires_each_listening_group_and_required_image_before_publish()
    {
        var exam = Exam.Create("LR-2026-MEDIA", "Media workspace", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);

        revision.ConfigureQuestionImageRequirement(101, true);

        var errors = revision.GetPublicationErrors(scoreProfileIsComplete: true);

        Assert.Contains(errors, error => error.Contains("P1-Q1", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Question 101", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("Master Audio", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(42_500)]
    [InlineData(3_600_000)]
    public void Audio_media_asset_accepts_valid_duration_bounds(int? durationMs)
    {
        var asset = ExamMediaAsset.Create("exam-media/audio.mp3", "audio/mpeg", DateTimeOffset.UtcNow,
            ExamMediaKind.Audio, 1024, durationMs);

        Assert.Equal(durationMs, asset.DurationMs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3_600_001)]
    public void Audio_media_asset_rejects_out_of_range_duration(int durationMs)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamMediaAsset.Create("exam-media/audio.mp3", "audio/mpeg",
            DateTimeOffset.UtcNow, ExamMediaKind.Audio, 1024, durationMs));
    }

    [Fact]
    public void Image_media_asset_rejects_non_null_duration()
    {
        Assert.Throws<ArgumentException>(() => ExamMediaAsset.Create("exam-media/image.png", "image/png",
            DateTimeOffset.UtcNow, ExamMediaKind.Image, 1024, 1000));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(42_500)]
    [InlineData(3_600_000)]
    public void Audio_upload_intent_accepts_valid_expected_duration_bounds(int? expectedDurationMs)
    {
        var now = DateTimeOffset.UtcNow;
        var intent = ExamMediaUploadIntent.Create(Guid.NewGuid(), Guid.NewGuid(), ExamMediaOwnerKind.AudioGroup,
            "P1-Q1", "exam-media/audio.mp3", "audio/mpeg", 1024, now.AddMinutes(15), now, expectedDurationMs);

        Assert.Equal(expectedDurationMs, intent.ExpectedDurationMs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3_600_001)]
    public void Audio_upload_intent_rejects_out_of_range_expected_duration(int expectedDurationMs)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamMediaUploadIntent.Create(Guid.NewGuid(), Guid.NewGuid(),
            ExamMediaOwnerKind.AudioGroup, "P1-Q1", "exam-media/audio.mp3", "audio/mpeg", 1024, now.AddMinutes(15),
            now, expectedDurationMs));
    }

    [Theory]
    [InlineData(ExamMediaOwnerKind.QuestionImage, "1")]
    [InlineData(ExamMediaOwnerKind.ContextImage, "P7-G147:1")]
    public void Image_upload_intent_rejects_non_null_expected_duration(ExamMediaOwnerKind ownerKind, string ownerKey)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => ExamMediaUploadIntent.Create(Guid.NewGuid(), Guid.NewGuid(),
            ownerKind, ownerKey, "exam-media/image.png", "image/png", 1024, now.AddMinutes(15), now, 1000));
    }
}
