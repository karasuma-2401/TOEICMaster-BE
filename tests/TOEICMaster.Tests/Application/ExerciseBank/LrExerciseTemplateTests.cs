using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Infrastructure.Persistence.ExerciseBank;

namespace TOEICMaster.Tests.Application.ExerciseBank;

public sealed class LrExerciseTemplateTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 1)]
    [InlineData(6, 4)]
    [InlineData(7, 3)]
    public void Shipped_template_imports_into_a_valid_item(int part, int questionCount)
    {
        var name = $"toeic-lr-part-{part}-import-template.xlsx";
        var file = Path.Combine(AppContext.BaseDirectory, "Resources", "ExerciseBankTemplates", name);
        var inputs = new XlsxLrExerciseBankImportParser().Parse(part, name, File.ReadAllBytes(file));
        var item = Assert.Single(inputs);
        Assert.True(new LrExerciseItemInputValidator().Validate(item).IsValid);
        Assert.Equal(questionCount, item.ToEntity(part, Guid.NewGuid(), DateTimeOffset.UtcNow).Questions.Count);
        if (part == 2)
            Assert.Null(Assert.Single(item.Questions).OptionD);
    }
}
