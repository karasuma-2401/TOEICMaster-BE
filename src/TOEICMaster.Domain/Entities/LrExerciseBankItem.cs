namespace TOEICMaster.Domain.Entities;

public enum LrExercisePart
{
    Part1 = 1,
    Part2 = 2,
    Part3 = 3,
    Part4 = 4,
    Part5 = 5,
    Part6 = 6,
    Part7 = 7
}

public enum LrExerciseItemKind
{
    Single = 1,
    Group = 2
}

public enum LrExerciseDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public enum LrExerciseStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public sealed record LrExerciseBankQuestionInput(string Code, string Prompt, string OptionA, string OptionB,
    string OptionC, string? OptionD, string CorrectKey, string? Translation, string? Explanation);

public sealed class LrExerciseBankItem
{
    private readonly List<LrExerciseBankQuestion> _questions = [];

    private LrExerciseBankItem()
    {
    }

    private LrExerciseBankItem(Guid id, LrExercisePart part, string code, string topic,
        LrExerciseDifficulty difficulty, LrExerciseStatus status, string? context, string? audioUrl, string? imageUrl,
        Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        Part = part;
        Kind = GetKind(part);
        Code = code;
        Topic = topic;
        Difficulty = difficulty;
        Status = status;
        Context = context;
        AudioUrl = audioUrl;
        ImageUrl = imageUrl;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public LrExercisePart Part { get; private set; }
    public LrExerciseItemKind Kind { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public LrExerciseDifficulty Difficulty { get; private set; }
    public LrExerciseStatus Status { get; private set; }
    public string? Context { get; private set; }
    public string? AudioUrl { get; private set; }
    public string? ImageUrl { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<LrExerciseBankQuestion> Questions => _questions.AsReadOnly();

    public static LrExerciseBankItem Create(LrExercisePart part, string code, string topic,
        LrExerciseDifficulty difficulty, LrExerciseStatus status, string? context, string? audioUrl, string? imageUrl,
        IReadOnlyCollection<LrExerciseBankQuestionInput> questions, Guid actorUserId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);
        Validate(part, code, topic, context, questions);
        var item = new LrExerciseBankItem(Guid.NewGuid(), part, code.Trim(), topic.Trim(), difficulty, status,
            TrimOrNull(context), TrimOrNull(audioUrl), TrimOrNull(imageUrl), actorUserId, now);
        item._questions.AddRange(questions.Select((question, index) => LrExerciseBankQuestion.Create(item.Id,
            index + 1, question, part)));
        return item;
    }

    public static LrExerciseItemKind GetKind(LrExercisePart part) => part is LrExercisePart.Part1 or
        LrExercisePart.Part2 or LrExercisePart.Part5 ? LrExerciseItemKind.Single : LrExerciseItemKind.Group;

    private static void Validate(LrExercisePart part, string code, string topic, string? context,
        IReadOnlyCollection<LrExerciseBankQuestionInput> questions)
    {
        if (!Enum.IsDefined(part))
            throw new ArgumentOutOfRangeException(nameof(part));
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 64)
            throw new ArgumentException("Item code is required and is at most 64 characters.", nameof(code));
        if (string.IsNullOrWhiteSpace(topic) || topic.Trim().Length > 100)
            throw new ArgumentException("Topic is required and is at most 100 characters.", nameof(topic));
        ArgumentNullException.ThrowIfNull(questions);
        var validQuestionCount = part switch
        {
            LrExercisePart.Part1 or LrExercisePart.Part2 or LrExercisePart.Part5 => questions.Count == 1,
            LrExercisePart.Part3 or LrExercisePart.Part4 => questions.Count == 3,
            LrExercisePart.Part6 => questions.Count == 4,
            LrExercisePart.Part7 => questions.Count is >= 2 and <= 5,
            _ => false
        };
        if (!validQuestionCount)
            throw new ArgumentException(QuestionCountMessage(part), nameof(questions));
        if (GetKind(part) == LrExerciseItemKind.Group && string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("A grouped item requires shared context.", nameof(context));
    }

    private static string QuestionCountMessage(LrExercisePart part) => part switch
    {
        LrExercisePart.Part3 or LrExercisePart.Part4 => "Parts 3 and 4 require exactly three child questions.",
        LrExercisePart.Part6 => "Part 6 requires exactly four child questions.",
        LrExercisePart.Part7 => "Part 7 requires from two to five child questions.",
        _ => "This part requires exactly one question."
    };

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class LrExerciseBankQuestion
{
    private LrExerciseBankQuestion()
    {
    }

    private LrExerciseBankQuestion(Guid id, Guid itemId, int ordinal, string code, string prompt, string optionA,
        string optionB, string optionC, string? optionD, string correctKey, string? translation, string? explanation)
    {
        Id = id;
        ItemId = itemId;
        Ordinal = ordinal;
        Code = code;
        Prompt = prompt;
        OptionA = optionA;
        OptionB = optionB;
        OptionC = optionC;
        OptionD = optionD;
        CorrectKey = correctKey;
        Translation = translation;
        Explanation = explanation;
    }

    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public int Ordinal { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Prompt { get; private set; } = string.Empty;
    public string OptionA { get; private set; } = string.Empty;
    public string OptionB { get; private set; } = string.Empty;
    public string OptionC { get; private set; } = string.Empty;
    public string? OptionD { get; private set; }
    public string CorrectKey { get; private set; } = string.Empty;
    public string? Translation { get; private set; }
    public string? Explanation { get; private set; }

    internal static LrExerciseBankQuestion Create(Guid itemId, int ordinal, LrExerciseBankQuestionInput input,
        LrExercisePart part)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 64 ||
            string.IsNullOrWhiteSpace(input.Prompt) || input.Prompt.Trim().Length > 4_000 ||
            string.IsNullOrWhiteSpace(input.OptionA) || string.IsNullOrWhiteSpace(input.OptionB) ||
            string.IsNullOrWhiteSpace(input.OptionC))
            throw new ArgumentException("Question code, prompt, and options A-C are required.", nameof(input));
        var optionD = TrimOrNull(input.OptionD);
        var correctKey = input.CorrectKey?.Trim().ToUpperInvariant();
        if (part == LrExercisePart.Part2)
        {
            if (optionD is not null || correctKey is not ("A" or "B" or "C"))
                throw new ArgumentException("Part 2 has exactly three options and a key from A to C.", nameof(input));
        }
        else if (optionD is null || correctKey is not ("A" or "B" or "C" or "D"))
            throw new ArgumentException("This part requires four options and a key from A to D.", nameof(input));
        return new LrExerciseBankQuestion(Guid.NewGuid(), itemId, ordinal, input.Code.Trim(), input.Prompt.Trim(),
            input.OptionA.Trim(), input.OptionB.Trim(), input.OptionC.Trim(), optionD, correctKey,
            TrimOrNull(input.Translation), TrimOrNull(input.Explanation));
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
