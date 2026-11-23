using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Seeders;

public static class ExamDataSeeder
{
    private const long SampleExamSeedLockKey = 0x544F454943534545;

    public static async Task SeedSampleExamAsync(ToeicDbContext db, CancellationToken cancellationToken = default,
        TimeProvider? timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var executionStrategy = db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async retryToken =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(retryToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({SampleExamSeedLockKey})", retryToken);
            try
            {
                await SeedSampleExamCoreAsync(db, clock.GetUtcNow(), retryToken);
                await transaction.CommitAsync(retryToken);
            }
            catch
            {
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);
    }

    private static async Task SeedSampleExamCoreAsync(ToeicDbContext db, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await db.Exams.AnyAsync(cancellationToken))
            return;

        var profile = ScoreProfile.Create("ETS TOEIC Standard Score Profile", now);

        for (int i = 0; i <= 100; i++)
        {
            int score = i == 0 ? 5 : Math.Min(495, (int)(Math.Round((i * 4.95) / 5.0) * 5));
            profile.SetScore(ExamSkill.Listening, i, score);
            profile.SetScore(ExamSkill.Reading, i, score);
        }

        var exam = Exam.Create("TOEIC-SAMPLE-01", "TOEIC Mini Practice Test 2026", profile.Id, now);
        var revision = exam.Revisions.First();

        var p1Block = revision.Blocks.Single(block => block.Part == 1 && block.FirstQuestionNumber == 1);

        revision.UpdateQuestion(1, new ExamQuestionContent(
            Code: "P1_Q01",
            Stem: "Look at the picture marked number 1 in your test book.",
            OptionA: "(A) A woman is typing on a laptop.",
            OptionB: "(B) A woman is pouring a cup of coffee.",
            OptionC: "(C) A woman is standing by the window.",
            OptionD: "(D) A woman is writing on a whiteboard.",
            CorrectKey: "A",
            Translation: "(A) A woman is typing on a laptop.",
            Explanation: "In the picture, a woman is sitting at work and typing on a laptop.",
            ImageAssetId: null
        ));

        revision.UpdateQuestion(2, new ExamQuestionContent(
            Code: "P1_Q02",
            Stem: "Look at the picture marked number 2 in your test book.",
            OptionA: "(A) Customers are waiting in line.",
            OptionB: "(B) Some boxes are stacked on a shelf.",
            OptionC: "(C) The door is being opened.",
            OptionD: "(D) A worker is sweeping the floor.",
            CorrectKey: "B",
            Translation: "(B) Some boxes are stacked on a bookshelf/shelf.",
            Explanation: "The boxes are stacked neatly on the display shelf.",
            ImageAssetId: null
        ));

        var p2Block = revision.Blocks.Single(block => block.Part == 2 && block.FirstQuestionNumber == 7);

        revision.UpdateQuestion(7, new ExamQuestionContent(
            Code: "P2_Q03",
            Stem: "Where is the marketing conference being held this year?",
            OptionA: "(A) In Toronto, Canada.",
            OptionB: "(B) Yes, I attended yesterday.",
            OptionC: "(C) At 9:00 AM sharp.",
            OptionD: null,
            CorrectKey: "A",
            Translation: "Where is the marketing conference being held this year? - (A) In Toronto, Canada.",
            Explanation: "A 'Where' question asks about a location, so the best answer is (A).",
            ImageAssetId: null
        ));

        var p5Block = revision.Blocks.Single(block => block.Part == 5 && block.FirstQuestionNumber == 101);

        revision.UpdateQuestion(101, new ExamQuestionContent(
            Code: "P5_Q101",
            Stem: "Ms. Tanaka requested that the financial report be submitted ------- Friday morning.",
            OptionA: "before",
            OptionB: "early",
            OptionC: "prior",
            OptionD: "ahead",
            CorrectKey: "A",
            Translation: "Ms. Tanaka requested that the financial report be submitted before Friday morning.",
            Explanation: "A time preposition is needed before the noun phrase 'Friday morning'. Choose 'before'.",
            ImageAssetId: null
        ));

        revision.UpdateQuestion(102, new ExamQuestionContent(
            Code: "P5_Q102",
            Stem: "The new ventilation system operates much more ------- than the previous model.",
            OptionA: "quiet",
            OptionB: "quietly",
            OptionC: "quietness",
            OptionD: "quietest",
            CorrectKey: "B",
            Translation: "The new ventilation system operates much more quietly than the previous model.",
            Explanation: "The action verb 'operates' is modified by the adverb 'quietly'.",
            ImageAssetId: null
        ));

        var p7Block = revision.Blocks.Single(block => block.Part == 7 && block.FirstQuestionNumber == 147);

        revision.ReplaceContextBlocks("P7-G147", [new ExamContextContent(
            Ordinal: 1,
            Type: ExamContextType.Text,
            Text: "MEMORANDUM\nTo: All Staff\nFrom: Facilities Department\nSubject: Elevator Maintenance\n\nPlease be advised that the main elevator will undergo scheduled maintenance this Thursday from 1:00 PM to 4:00 PM. During this time, please use the stairs or the service elevator.",
            ImageAssetId: null,
            AltText: null
        )]);

        revision.UpdateQuestion(147, new ExamQuestionContent(
            Code: "P7_Q147",
            Stem: "What is the purpose of the memorandum?",
            OptionA: "(A) To announce a company holiday",
            OptionB: "(B) To inform staff about temporary elevator maintenance",
            OptionC: "(C) To request volunteers for an inspection",
            OptionD: "(D) To introduce a new facility manager",
            CorrectKey: "B",
            Translation: "What is the purpose of the memorandum? - (B) To inform staff about elevator maintenance.",
            Explanation: "The passage states that Elevator Maintenance will take place on Thursday.",
            ImageAssetId: null
        ));

        revision.UpdateQuestion(148, new ExamQuestionContent(
            Code: "P7_Q148",
            Stem: "What are employees advised to do during the inspection?",
            OptionA: "(A) Work from home",
            OptionB: "(B) Postpone all client meetings",
            OptionC: "(C) Use the stairs or service elevator",
            OptionD: "(D) Park at the rear entrance",
            CorrectKey: "C",
            Translation: "What are employees advised to do? - (C) Use the stairs or the service elevator.",
            Explanation: "The last sentence of the passage clearly states: 'please use the stairs or the service elevator.'",
            ImageAssetId: null
        ));

        exam.PublishDraft(revision.Id);

        db.ScoreProfiles.Add(profile);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(cancellationToken);
    }
}
