using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.Exams;

public sealed class ExamAggregateTests
{
    [Fact]
    public void New_LR_exam_creates_one_draft_revision_with_the_fixed_ETS_200_slot_structure()
    {
        var exam = Exam.Create("LR-2026-001", "Practice test 1", Guid.NewGuid(), DateTimeOffset.UtcNow);

        var revision = Assert.Single(exam.Revisions);
        Assert.Equal(1, revision.VersionNumber);
        Assert.Equal(ExamRevisionStatus.Draft, revision.Status);
        Assert.Equal(200, revision.Blocks.Sum(block => block.Questions.Count));
        Assert.Equal(new[] { 6, 25, 39, 30, 30, 16, 54 }, revision.Blocks
            .GroupBy(block => block.Part).OrderBy(group => group.Key)
            .Select(group => group.Sum(block => block.Questions.Count)));
        Assert.Equal(13, revision.Blocks.Count(block => block.Part == 3 && block.IsGrouped));
        Assert.Equal(10, revision.Blocks.Count(block => block.Part == 4 && block.IsGrouped));
        Assert.Equal(new[] { 2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, 5 }, revision.Blocks
            .Where(block => block.Part == 7).Select(block => block.Questions.Count));
    }

    [Fact]
    public void Published_revision_is_preserved_when_admin_forks_a_new_draft_revision()
    {
        var exam = Exam.Create("LR-2026-001", "Practice test 1", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var published = Assert.Single(exam.Revisions);
        published.Publish();

        var nextDraft = exam.CreateDraftRevision(published.Id, DateTimeOffset.UtcNow);

        Assert.Equal(ExamRevisionStatus.Published, published.Status);
        Assert.Equal(1, published.VersionNumber);
        Assert.Equal(2, nextDraft.VersionNumber);
        Assert.Equal(ExamRevisionStatus.Draft, nextDraft.Status);
        Assert.Equal(200, nextDraft.Blocks.Sum(block => block.Questions.Count));
        Assert.NotSame(published.Blocks.First(), nextDraft.Blocks.First());
    }
}
