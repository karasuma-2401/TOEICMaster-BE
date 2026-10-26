using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.Exams;

public sealed class ExamRevisionEditingTests
{
    [Fact]
    public void Draft_accepts_three_options_for_a_part_2_question_and_blocks_edits_after_publish()
    {
        var exam = Exam.Create("LR-2026-001", "Practice test 1", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);
        var content = new ExamQuestionContent("LR-2026-001-007", "Where is the meeting?", "At noon.",
            "In room 4.", "With the manager.", null, "B", null, "Because the speaker names room 4.", null);

        revision.UpdateQuestion(7, content);

        var question = revision.Blocks.Single(block => block.Key == "P2-Q7").Questions.Single();
        Assert.Equal("LR-2026-001-007", question.Code);
        Assert.Equal("B", question.CorrectKey);
        Assert.Null(question.OptionD);

        revision.Publish();
        Assert.Throws<InvalidOperationException>(() => revision.UpdateQuestion(7, content));
    }

    [Fact]
    public void Group_context_requires_ordered_nonempty_text_or_a_media_asset_reference()
    {
        var exam = Exam.Create("LR-2026-001", "Practice test 1", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);

        revision.ReplaceContextBlocks("P3-G32", [
            new ExamContextContent(1, ExamContextType.Text, "Transcript available for review.", null, null),
            new ExamContextContent(2, ExamContextType.Image, null, Guid.NewGuid(), "Conversation diagram")
        ]);

        var block = revision.Blocks.Single(candidate => candidate.Key == "P3-G32");
        Assert.Equal(2, block.ContextBlocks.Count);
        Assert.Throws<ArgumentException>(() => revision.ReplaceContextBlocks("P3-G32", [
            new ExamContextContent(2, ExamContextType.Text, "Out of order", null, null)
        ]));
    }

    [Fact]
    public void Publish_validation_reports_missing_audio_score_profile_and_question_content()
    {
        var exam = Exam.Create("LR-2026-001", "Practice test 1", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var revision = Assert.Single(exam.Revisions);

        var errors = revision.GetPublicationErrors(scoreProfileIsComplete: false);

        Assert.Contains(errors, error => error.Contains("Master Audio", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("barem", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Question 1", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("P3-G32", StringComparison.Ordinal));
    }
}
