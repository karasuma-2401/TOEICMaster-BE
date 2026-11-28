using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.ExerciseBank;

public sealed class LrExerciseBankItemTests
{
    [Fact]
    public void Published_part_one_requires_audio_and_image_but_draft_can_be_incomplete()
    {
        var questions = new[] { new LrExerciseBankQuestionInput("P1-Q1", "What is shown?", "A", "B", "C", "D", "A", null, null) };
        LrExerciseBankItem.Create(LrExercisePart.Part1, "P1-DRAFT", "Office", LrExerciseDifficulty.Easy,
            LrExerciseStatus.Draft, null, null, null, questions, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => LrExerciseBankItem.Create(LrExercisePart.Part1, "P1-PUBLISHED",
            "Office", LrExerciseDifficulty.Easy, LrExerciseStatus.Published, null, null, null, questions,
            Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => LrExerciseBankItem.Create(LrExercisePart.Part1, "P1-PUBLISHED",
            "Office", LrExerciseDifficulty.Easy, LrExerciseStatus.Published, null, "audio.mp3", null, questions,
            Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Draft_can_attach_media_and_publish_with_a_new_version()
    {
        var owner = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var questions = new[] { new LrExerciseBankQuestionInput("P1-Q2", "What is shown?", "A", "B", "C", "D", "A", null, null) };
        var item = LrExerciseBankItem.Create(LrExercisePart.Part1, "P1-DRAFT-2", "Office",
            LrExerciseDifficulty.Easy, LrExerciseStatus.Draft, null, null, null, questions, owner, now);
        Assert.Throws<ArgumentException>(() => item.UpdateMediaAndStatus("audio.mp3", null,
            LrExerciseStatus.Published, owner, now.AddMinutes(1)));
        Assert.Equal(LrExerciseStatus.Draft, item.Status);
        Assert.Equal(1, item.Version);
        item.UpdateMediaAndStatus("audio.mp3", "image.png", LrExerciseStatus.Published, owner,
            now.AddMinutes(1));
        Assert.Equal(LrExerciseStatus.Published, item.Status);
        Assert.Equal(2, item.Version);
        Assert.Equal(now.AddMinutes(1), item.UpdatedAtUtc);
    }
    [Fact]
    public void Grouped_part_creates_one_item_with_its_child_questions()
    {
        var now = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        var item = LrExerciseBankItem.Create(LrExercisePart.Part3, "P3-G-001", "Office", LrExerciseDifficulty.Medium,
            LrExerciseStatus.Published, "Man: Did you send the report?", "audio.mp3", null,
            [
                new LrExerciseBankQuestionInput("P3-001", "What are they discussing?", "A report", "A flight", "A meeting", "A lunch", "A", null, null),
                new LrExerciseBankQuestionInput("P3-002", "What does the man ask?", "A report", "A flight", "A meeting", "A lunch", "B", null, null),
                new LrExerciseBankQuestionInput("P3-003", "What will they do next?", "A report", "A flight", "A meeting", "A lunch", "C", null, null)
            ], Guid.NewGuid(), now);

        Assert.Equal(LrExerciseItemKind.Group, item.Kind);
        Assert.Equal(3, (int)item.Part);
        Assert.Equal(3, item.Questions.Count);
        Assert.Equal("P3-001", item.Questions.First().Code);
    }

    [Fact]
    public void Part_three_rejects_a_group_without_three_child_questions()
    {
        var question = new LrExerciseBankQuestionInput("P3-001", "What are they discussing?", "A report",
            "A flight", "A meeting", "A lunch", "A", null, null);

        Assert.Throws<ArgumentException>(() => LrExerciseBankItem.Create(LrExercisePart.Part3, "P3-G-002", "Office",
            LrExerciseDifficulty.Medium, LrExerciseStatus.Draft, "Shared context", null, null, [question],
            Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Single_part_rejects_multiple_questions()
    {
        var questions = new[]
        {
            new LrExerciseBankQuestionInput("P5-001", "The client ___ the invoice.", "sent", "send", "sending", "sends", "A", null, null),
            new LrExerciseBankQuestionInput("P5-002", "The manager ___ today.", "arrive", "arrives", "arrived", "arriving", "B", null, null)
        };

        Assert.Throws<ArgumentException>(() => LrExerciseBankItem.Create(LrExercisePart.Part5, "P5-001", "Grammar",
            LrExerciseDifficulty.Easy, LrExerciseStatus.Draft, null, null, null, questions, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }
}
