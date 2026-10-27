namespace TOEICMaster.Domain.Entities;

public enum ExamRevisionStatus
{
    Draft = 1,
    Published = 2,
    Superseded = 3
}

public enum ExamBlockType
{
    Single = 1,
    Group = 2
}

public enum ExamContextType
{
    Text = 1,
    Image = 2
}

public sealed record ExamQuestionContent(string Code, string Stem, string OptionA, string OptionB, string OptionC,
    string? OptionD, string CorrectKey, string? Translation, string? Explanation, Guid? ImageAssetId);

public sealed record ExamContextContent(int Ordinal, ExamContextType Type, string? Text, Guid? ImageAssetId,
    string? AltText);

public sealed class Exam
{
    private readonly List<ExamRevision> _revisions = [];

    private Exam()
    {
    }

    private Exam(Guid id, string code, string title, Guid scoreProfileId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Code = code;
        Title = title;
        CreatedAtUtc = createdAtUtc;
        _revisions.Add(ExamRevision.CreateInitial(id, scoreProfileId, createdAtUtc));
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ExamRevision> Revisions => _revisions.AsReadOnly();

    public static Exam Create(string code, string title, Guid scoreProfileId, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfEqual(scoreProfileId, Guid.Empty);
        return new Exam(Guid.NewGuid(), code.Trim(), title.Trim(), scoreProfileId, createdAtUtc);
    }

    public ExamRevision CreateDraftRevision(Guid sourceRevisionId, DateTimeOffset createdAtUtc)
    {
        var source = _revisions.SingleOrDefault(revision => revision.Id == sourceRevisionId)
            ?? throw new InvalidOperationException("The source revision does not belong to this exam.");
        if (source.Status != ExamRevisionStatus.Published)
            throw new InvalidOperationException("Only a published revision can be forked.");

        var draft = source.Clone(Guid.NewGuid(), _revisions.Max(revision => revision.VersionNumber) + 1, createdAtUtc);
        _revisions.Add(draft);
        return draft;
    }

    public void PublishDraft(Guid revisionId)
    {
        var draft = _revisions.SingleOrDefault(revision => revision.Id == revisionId)
            ?? throw new InvalidOperationException("The revision does not belong to this exam.");
        if (draft.Status != ExamRevisionStatus.Draft)
            throw new InvalidOperationException("Only a draft revision can be published.");

        foreach (var published in _revisions.Where(revision => revision.Status == ExamRevisionStatus.Published))
            published.Supersede();
        draft.Publish();
    }
}

public sealed class ExamRevision
{
    private readonly List<ExamBlock> _blocks = [];

    private ExamRevision()
    {
    }

    private ExamRevision(Guid id, Guid examId, int versionNumber, ExamRevisionStatus status, Guid scoreProfileId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ExamId = examId;
        VersionNumber = versionNumber;
        Status = status;
        ScoreProfileId = scoreProfileId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public int VersionNumber { get; private set; }
    public ExamRevisionStatus Status { get; private set; }
    public Guid ScoreProfileId { get; private set; }
    public string? MasterAudioObjectKey { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ExamBlock> Blocks => _blocks.AsReadOnly();

    internal static ExamRevision CreateInitial(Guid examId, Guid scoreProfileId, DateTimeOffset createdAtUtc)
    {
        var revision = new ExamRevision(Guid.NewGuid(), examId, 1, ExamRevisionStatus.Draft, scoreProfileId, createdAtUtc);
        revision.AddSkeleton();
        return revision;
    }

    internal ExamRevision Clone(Guid id, int versionNumber, DateTimeOffset createdAtUtc)
    {
        var clone = new ExamRevision(id, ExamId, versionNumber, ExamRevisionStatus.Draft, ScoreProfileId, createdAtUtc)
        {
            MasterAudioObjectKey = MasterAudioObjectKey
        };
        clone._blocks.AddRange(_blocks.Select(block => block.Clone(id)));
        return clone;
    }

    public void Publish()
    {
        if (Status != ExamRevisionStatus.Draft)
            throw new InvalidOperationException("Only a draft revision can be published.");
        Status = ExamRevisionStatus.Published;
    }

    public void UpdateQuestion(int questionNumber, ExamQuestionContent content)
    {
        EnsureDraft();
        var block = _blocks.SingleOrDefault(candidate => candidate.Questions.Any(question => question.Number == questionNumber))
            ?? throw new ArgumentOutOfRangeException(nameof(questionNumber));
        ValidateQuestionContent(block.Part, content);
        block.Questions.Single(question => question.Number == questionNumber).Configure(content);
    }

    public void ReplaceContextBlocks(string blockKey, IReadOnlyCollection<ExamContextContent> contexts)
    {
        EnsureDraft();
        var block = _blocks.SingleOrDefault(candidate => candidate.Key == blockKey)
            ?? throw new ArgumentException("Unknown block key.", nameof(blockKey));
        if (!block.IsGrouped)
            throw new ArgumentException("Only grouped blocks can have shared contexts.", nameof(blockKey));
        block.ReplaceContextBlocks(contexts);
    }

    public void SetMasterAudioObjectKey(string objectKey)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        MasterAudioObjectKey = objectKey;
    }

    public void SetScoreProfile(Guid scoreProfileId)
    {
        EnsureDraft();
        ArgumentOutOfRangeException.ThrowIfEqual(scoreProfileId, Guid.Empty);
        ScoreProfileId = scoreProfileId;
    }

    public IReadOnlyList<string> GetPublicationErrors(bool scoreProfileIsComplete)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(MasterAudioObjectKey))
            errors.Add("Master Audio is required.");
        if (!scoreProfileIsComplete)
            errors.Add("Barem điểm chưa đủ 101 mốc cho Listening và Reading.");

        foreach (var block in _blocks)
        {
            if (block.IsGrouped && block.ContextBlocks.Count == 0)
                errors.Add($"Block {block.Key} requires at least one shared context.");
            foreach (var question in block.Questions)
            {
                if (!IsQuestionComplete(block.Part, question))
                    errors.Add($"Question {question.Number} is incomplete.");
                if (block.Part == 1 && (question.ImageAssetId is null || question.ImageAssetId == Guid.Empty))
                    errors.Add($"Question {question.Number} requires an image asset.");
            }
        }

        var duplicateCodes = _blocks.SelectMany(block => block.Questions).Where(question => question.Code is not null)
            .GroupBy(question => question.Code!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1);
        errors.AddRange(duplicateCodes.Select(group => $"Question code '{group.Key}' is duplicated."));
        return errors;
    }

    internal void Supersede()
    {
        if (Status == ExamRevisionStatus.Published)
            Status = ExamRevisionStatus.Superseded;
    }

    private void AddSkeleton()
    {
        AddSingles(1, 1, 6);
        AddSingles(2, 7, 25);
        AddGroups(3, 32, Enumerable.Repeat(3, 13));
        AddGroups(4, 71, Enumerable.Repeat(3, 10));
        AddSingles(5, 101, 30);
        AddGroups(6, 131, Enumerable.Repeat(4, 4));
        AddGroups(7, 147, [2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, 5]);
    }

    private void EnsureDraft()
    {
        if (Status != ExamRevisionStatus.Draft)
            throw new InvalidOperationException("Only a draft revision can be edited.");
    }

    private static void ValidateQuestionContent(int part, ExamQuestionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(content.Code) || string.IsNullOrWhiteSpace(content.Stem) ||
            string.IsNullOrWhiteSpace(content.OptionA) || string.IsNullOrWhiteSpace(content.OptionB) ||
            string.IsNullOrWhiteSpace(content.OptionC))
            throw new ArgumentException("Question code, stem, and options A-C are required.", nameof(content));

        var correctKey = content.CorrectKey?.Trim().ToUpperInvariant();
        if (part == 2)
        {
            if (!string.IsNullOrWhiteSpace(content.OptionD) || correctKey is not ("A" or "B" or "C"))
                throw new ArgumentException("Part 2 has exactly three options and a key from A to C.", nameof(content));
            return;
        }

        if (string.IsNullOrWhiteSpace(content.OptionD) || correctKey is not ("A" or "B" or "C" or "D"))
            throw new ArgumentException("This part requires four options and a key from A to D.", nameof(content));
    }

    private static bool IsQuestionComplete(int part, ExamQuestion question)
    {
        if (string.IsNullOrWhiteSpace(question.Code) || string.IsNullOrWhiteSpace(question.Stem) ||
            string.IsNullOrWhiteSpace(question.OptionA) || string.IsNullOrWhiteSpace(question.OptionB) ||
            string.IsNullOrWhiteSpace(question.OptionC))
            return false;
        return part == 2
            ? question.OptionD is null && question.CorrectKey is "A" or "B" or "C"
            : !string.IsNullOrWhiteSpace(question.OptionD) && question.CorrectKey is "A" or "B" or "C" or "D";
    }

    private void AddSingles(int part, int firstQuestionNumber, int count)
    {
        for (var questionNumber = firstQuestionNumber; questionNumber < firstQuestionNumber + count; questionNumber++)
            _blocks.Add(ExamBlock.Create(Id, $"P{part}-Q{questionNumber}", part, ExamBlockType.Single,
                questionNumber, 1));
    }

    private void AddGroups(int part, int firstQuestionNumber, IEnumerable<int> groupQuestionCounts)
    {
        var nextQuestionNumber = firstQuestionNumber;
        foreach (var questionCount in groupQuestionCounts)
        {
            _blocks.Add(ExamBlock.Create(Id, $"P{part}-G{nextQuestionNumber}", part, ExamBlockType.Group,
                nextQuestionNumber, questionCount));
            nextQuestionNumber += questionCount;
        }
    }
}

public sealed class ExamBlock
{
    private readonly List<ExamQuestion> _questions = [];
    private readonly List<ExamContextBlock> _contextBlocks = [];

    private ExamBlock()
    {
    }

    private ExamBlock(Guid id, Guid revisionId, string key, int part, ExamBlockType type, int firstQuestionNumber)
    {
        Id = id;
        RevisionId = revisionId;
        Key = key;
        Part = part;
        Type = type;
        FirstQuestionNumber = firstQuestionNumber;
    }

    public Guid Id { get; private set; }
    public Guid RevisionId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public int Part { get; private set; }
    public ExamBlockType Type { get; private set; }
    public int FirstQuestionNumber { get; private set; }
    public bool IsGrouped => Type == ExamBlockType.Group;
    public IReadOnlyCollection<ExamQuestion> Questions => _questions.AsReadOnly();
    public IReadOnlyCollection<ExamContextBlock> ContextBlocks => _contextBlocks.AsReadOnly();

    internal static ExamBlock Create(Guid revisionId, string key, int part, ExamBlockType type,
        int firstQuestionNumber, int questionCount)
    {
        var block = new ExamBlock(Guid.NewGuid(), revisionId, key, part, type, firstQuestionNumber);
        for (var offset = 0; offset < questionCount; offset++)
            block._questions.Add(ExamQuestion.Create(block.Id, firstQuestionNumber + offset));
        return block;
    }

    internal ExamBlock Clone(Guid revisionId)
    {
        var clone = new ExamBlock(Guid.NewGuid(), revisionId, Key, Part, Type, FirstQuestionNumber);
        clone._questions.AddRange(_questions.Select(question => question.Clone(clone.Id)));
        clone._contextBlocks.AddRange(_contextBlocks.Select(context => context.Clone(clone.Id)));
        return clone;
    }

    internal void ReplaceContextBlocks(IReadOnlyCollection<ExamContextContent> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        if (contexts.Count == 0)
            throw new ArgumentException("A grouped block requires at least one context block.", nameof(contexts));

        var ordered = contexts.OrderBy(context => context.Ordinal).ToArray();
        if (ordered.Select(context => context.Ordinal).SequenceEqual(Enumerable.Range(1, ordered.Length)) is false)
            throw new ArgumentException("Context ordinals must start at 1 and be contiguous.", nameof(contexts));
        foreach (var context in ordered)
        {
            if (context.Type == ExamContextType.Text && string.IsNullOrWhiteSpace(context.Text))
                throw new ArgumentException("A text context requires text.", nameof(contexts));
            if (context.Type == ExamContextType.Image &&
                (context.ImageAssetId is null || context.ImageAssetId == Guid.Empty))
                throw new ArgumentException("An image context requires an image asset.", nameof(contexts));
        }

        _contextBlocks.Clear();
        _contextBlocks.AddRange(ordered.Select(context => ExamContextBlock.Create(Id, context)));
    }
}

public sealed class ExamQuestion
{
    private ExamQuestion()
    {
    }

    private ExamQuestion(Guid id, Guid blockId, int number)
    {
        Id = id;
        BlockId = blockId;
        Number = number;
    }

    public Guid Id { get; private set; }
    public Guid BlockId { get; private set; }
    public int Number { get; private set; }
    public string? Code { get; private set; }
    public string? Stem { get; private set; }
    public string? OptionA { get; private set; }
    public string? OptionB { get; private set; }
    public string? OptionC { get; private set; }
    public string? OptionD { get; private set; }
    public string? CorrectKey { get; private set; }
    public string? Translation { get; private set; }
    public string? Explanation { get; private set; }
    public Guid? ImageAssetId { get; private set; }

    internal static ExamQuestion Create(Guid blockId, int number) => new(Guid.NewGuid(), blockId, number);

    internal ExamQuestion Clone(Guid blockId)
    {
        var clone = new ExamQuestion(Guid.NewGuid(), blockId, Number);
        if (Code is not null)
            clone.Configure(new ExamQuestionContent(Code, Stem!, OptionA!, OptionB!, OptionC!, OptionD, CorrectKey!,
                Translation, Explanation, ImageAssetId));
        return clone;
    }

    internal void Configure(ExamQuestionContent content)
    {
        Code = content.Code.Trim();
        Stem = content.Stem.Trim();
        OptionA = content.OptionA.Trim();
        OptionB = content.OptionB.Trim();
        OptionC = content.OptionC.Trim();
        OptionD = string.IsNullOrWhiteSpace(content.OptionD) ? null : content.OptionD.Trim();
        CorrectKey = content.CorrectKey.Trim().ToUpperInvariant();
        Translation = string.IsNullOrWhiteSpace(content.Translation) ? null : content.Translation.Trim();
        Explanation = string.IsNullOrWhiteSpace(content.Explanation) ? null : content.Explanation.Trim();
        ImageAssetId = content.ImageAssetId;
    }
}

public sealed class ExamContextBlock
{
    private ExamContextBlock()
    {
    }

    private ExamContextBlock(Guid id, Guid blockId, int ordinal, ExamContextType type, string? text,
        Guid? imageAssetId, string? altText)
    {
        Id = id;
        BlockId = blockId;
        Ordinal = ordinal;
        Type = type;
        Text = text;
        ImageAssetId = imageAssetId;
        AltText = altText;
    }

    public Guid Id { get; private set; }
    public Guid BlockId { get; private set; }
    public int Ordinal { get; private set; }
    public ExamContextType Type { get; private set; }
    public string? Text { get; private set; }
    public Guid? ImageAssetId { get; private set; }
    public string? AltText { get; private set; }

    internal static ExamContextBlock Create(Guid blockId, ExamContextContent content) => new(Guid.NewGuid(), blockId,
        content.Ordinal, content.Type, string.IsNullOrWhiteSpace(content.Text) ? null : content.Text.Trim(),
        content.ImageAssetId, string.IsNullOrWhiteSpace(content.AltText) ? null : content.AltText.Trim());

    internal ExamContextBlock Clone(Guid blockId) => new(Guid.NewGuid(), blockId, Ordinal, Type, Text, ImageAssetId,
        AltText);
}
