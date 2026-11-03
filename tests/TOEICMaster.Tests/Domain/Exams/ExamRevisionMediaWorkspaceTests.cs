using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.Exams;

public sealed class ExamRevisionMediaWorkspaceTests
{
    [Fact]
    public void Media_workspace_requires_each_listening_group_and_required_reading_image_before_publish()
    {
        var exam = Exam.Create("LR-2026-MEDIA", "Media workspace", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);

        revision.EnableMediaWorkspace();
        revision.ConfigureQuestionImageRequirement(101, true);

        var errors = revision.GetPublicationErrors(scoreProfileIsComplete: true);

        Assert.Contains(errors, error => error.Contains("P1-Q1", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Question 101", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("Master Audio", StringComparison.Ordinal));
    }
}
