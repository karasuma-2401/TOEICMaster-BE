using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Seeders;

public static class ExamDataSeeder
{
    public static async Task SeedSampleExamAsync(ToeicDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Exams.AnyAsync(cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;

        var profile = ScoreProfile.Create("ETS TOEIC Standard Score Profile", now);

        for (int i = 0; i <= 100; i++)
        {
            int score = i == 0 ? 5 : Math.Min(495, (int)(Math.Round((i * 4.95) / 5.0) * 5));
            profile.SetScore(ExamSkill.Listening, i, score);
            profile.SetScore(ExamSkill.Reading, i, score);
        }

        db.ScoreProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

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
            Translation: "(A) Một phụ nữ đang gõ bàn phím trên máy tính xách tay.",
            Explanation: "Trong hình là người phụ nữ đang ngồi làm việc và gõ phím trên laptop.",
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
            Translation: "(B) Một số hộp được xếp trên giá sách/kệ.",
            Explanation: "Các thùng hàng được xếp chồng gọn gàng trên kệ trưng bày.",
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
            Translation: "Hội nghị tiếp thị năm nay được tổ chức ở đâu? - (A) Tại Toronto, Canada.",
            Explanation: "Câu hỏi Where hỏi về địa điểm, đáp án phù hợp nhất là (A).",
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
            Translation: "Bà Tanaka đã yêu cầu báo cáo tài chính phải được nộp trước sáng thứ Sáu.",
            Explanation: "Cần một giới từ chỉ thời gian đứng trước danh từ Friday morning. Chọn before.",
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
            Translation: "Hệ thống thông gió mới hoạt động êm ái hơn nhiều so với mẫu trước đây.",
            Explanation: "Động từ thường operates đi cùng trạng từ quietly để bổ nghĩa.",
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
            Translation: "Mục đích của thông báo là gì? - (B) Thông báo về việc bảo trì thang máy.",
            Explanation: "Đoạn văn thông báo Elevator Maintenance sẽ diễn ra vào thứ Năm.",
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
            Translation: "Nhân viên được khuyên làm gì? - (C) Dùng cầu thang bộ hoặc thang máy dịch vụ.",
            Explanation: "Câu cuối đoạn văn nêu rõ: please use the stairs or the service elevator.",
            ImageAssetId: null
        ));

        exam.PublishDraft(revision.Id);

        db.Exams.Add(exam);
        await db.SaveChangesAsync(cancellationToken);
    }
}
