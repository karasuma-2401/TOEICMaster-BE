using TOEICMaster.Application.Exams.Importing;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class CompleteExamWorkbookTemplateTests
{
    [Fact]
    public void Template_has_the_fixed_103_blocks_and_200_questions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "ExamTemplates",
            "toeic-lr-complete-exam-import-template.xlsx");

        var workbook = XlsxExamWorkbookReader.Read(File.ReadAllBytes(path));

        Assert.Equal("LR-DEMO-001", workbook.TestCode);
        Assert.Equal(103, workbook.Blocks.Count);
        Assert.Equal(200, workbook.Questions.Count);
        Assert.Equal(6, workbook.Questions.Count(question => question.QuestionNumber is >= 1 and <= 6));
        Assert.Equal(25, workbook.Questions.Count(question => question.QuestionNumber is >= 7 and <= 31));
        Assert.Equal(39, workbook.Questions.Count(question => question.QuestionNumber is >= 32 and <= 70));
        Assert.Equal(30, workbook.Questions.Count(question => question.QuestionNumber is >= 71 and <= 100));
        Assert.Equal(30, workbook.Questions.Count(question => question.QuestionNumber is >= 101 and <= 130));
        Assert.Equal(16, workbook.Questions.Count(question => question.QuestionNumber is >= 131 and <= 146));
        Assert.Equal(54, workbook.Questions.Count(question => question.QuestionNumber is >= 147 and <= 200));
    }
}
