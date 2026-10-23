using FluentValidation;
using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record CreateWritingExerciseCommand(Guid ActorUserId, WritingExerciseInput Input)
    : IRequest<WritingExerciseDto>;

public sealed record UpdateWritingExerciseCommand(Guid ActorUserId, Guid ExerciseId, int ExpectedVersion,
    WritingExerciseInput Input) : IRequest<WritingExerciseDto>;

public sealed record ArchiveWritingExerciseCommand(Guid ActorUserId, Guid ExerciseId, int ExpectedVersion) : IRequest;

public sealed class WritingExerciseInputValidator : AbstractValidator<WritingExerciseInput>
{
    public WritingExerciseInputValidator()
    {
        RuleFor(input => input.Part).InclusiveBetween(1, 3);
        RuleFor(input => input.Code).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(value => value is not null && value.Trim().Length <= 64);
        RuleFor(input => input.Topic).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(value => value is not null && value.Trim().Length <= 100);
        RuleFor(input => input.Difficulty).Must(IsDifficulty).WithMessage("Độ khó không hợp lệ.");
        RuleFor(input => input.Status).Must(IsExerciseStatus).WithMessage("Trạng thái không hợp lệ.");
        RuleFor(input => input.RubricNotes).Must(value => value is null || value.Trim().Length <= 4_000);
        RuleFor(input => input.ModelAnswer).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(value => value is not null && value.Trim().Length <= 12_000).WithMessage("Bài mẫu không hợp lệ.");
        RuleFor(input => input.ImageAlt).Must(value => value is null || value.Trim().Length <= 300);
        RuleFor(input => input.KeywordOne).Must(value => value is null || value.Trim().Length <= 100);
        RuleFor(input => input.KeywordTwo).Must(value => value is null || value.Trim().Length <= 100);
        RuleFor(input => input.Sender).Must(value => value is null || value.Trim().Length <= 200);
        RuleFor(input => input.Recipient).Must(value => value is null || value.Trim().Length <= 200);
        RuleFor(input => input.Subject).Must(value => value is null || value.Trim().Length <= 300);
        RuleFor(input => input.EmailBody).Must(value => value is null || value.Trim().Length <= 12_000);
        RuleFor(input => input.Directions).Must(value => value is null || value.Trim().Length <= 4_000);
        RuleFor(input => input.Prompt).Must(value => value is null || value.Trim().Length <= 4_000);
        RuleFor(input => input.Outline).Must(value => value is null || value.Trim().Length <= 8_000);
        RuleFor(input => input.MinimumWordCount).Must(value => value is null || value is >= 1 and <= 5_000)
            .WithMessage("Số từ tối thiểu phải từ 1 đến 5000.");

        When(input => input.Part == 1, () =>
        {
            RuleFor(input => input.ImageAssetId).NotNull().WithMessage("Part 1 cần ảnh đề bài.");
            RuleFor(input => input.ImageAlt).NotEmpty().WithMessage("Part 1 cần mô tả ảnh.");
            RuleFor(input => input.KeywordOne).NotEmpty().WithMessage("Part 1 cần từ gợi ý thứ nhất.");
            RuleFor(input => input.KeywordTwo).NotEmpty().WithMessage("Part 1 cần từ gợi ý thứ hai.");
        });
        When(input => input.Part == 2, () =>
        {
            RuleFor(input => input.Sender).NotEmpty().WithMessage("Part 2 cần người gửi.");
            RuleFor(input => input.Recipient).NotEmpty().WithMessage("Part 2 cần người nhận.");
            RuleFor(input => input.Subject).NotEmpty().WithMessage("Part 2 cần chủ đề email.");
            RuleFor(input => input.EmailBody).NotEmpty().WithMessage("Part 2 cần nội dung email.");
            RuleFor(input => input.Directions).NotEmpty().WithMessage("Part 2 cần yêu cầu phản hồi.");
        });
        When(input => input.Part == 3, () =>
        {
            RuleFor(input => input.Prompt).NotEmpty().WithMessage("Part 3 cần đề bài.");
            RuleFor(input => input.MinimumWordCount).NotNull().WithMessage("Part 3 cần số từ tối thiểu.");
        });
    }

    internal static bool IsDifficulty(string? value) => value is not null &&
        new[] { "EASY", "MEDIUM", "HARD" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    internal static bool IsExerciseStatus(string? value) => value is not null &&
        new[] { "DRAFT", "PUBLISHED", "ARCHIVED" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}

public sealed class CreateWritingExerciseCommandValidator : AbstractValidator<CreateWritingExerciseCommand>
{
    public CreateWritingExerciseCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Input).SetValidator(new WritingExerciseInputValidator());
    }
}

public sealed class UpdateWritingExerciseCommandValidator : AbstractValidator<UpdateWritingExerciseCommand>
{
    public UpdateWritingExerciseCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.ExerciseId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command.Input).SetValidator(new WritingExerciseInputValidator());
    }
}

public sealed class ArchiveWritingExerciseCommandValidator : AbstractValidator<ArchiveWritingExerciseCommand>
{
    public ArchiveWritingExerciseCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.ExerciseId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class CreateWritingExerciseCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateWritingExerciseCommand, WritingExerciseDto>
{
    public async Task<WritingExerciseDto> Handle(CreateWritingExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Input.Normalize();
        if (await repository.ExerciseCodeExistsAsync(input.Code, null, cancellationToken))
            throw new WritingConflictException("Mã bài Writing đã tồn tại.");
        await EnsureImageExistsAsync(input, repository, cancellationToken);
        var exercise = input.ToEntity(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.AddExerciseAsync(exercise, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return exercise.ToDto();
    }

    internal static async Task EnsureImageExistsAsync(WritingExerciseInput input, IWritingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        if (input.Part == 1 && (!input.ImageAssetId.HasValue ||
                                !await repository.MediaAssetExistsAsync(input.ImageAssetId.Value, cancellationToken)))
            throw new WritingConflictException("Ảnh đề bài không tồn tại.");
    }
}

public sealed class UpdateWritingExerciseCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateWritingExerciseCommand, WritingExerciseDto>
{
    public async Task<WritingExerciseDto> Handle(UpdateWritingExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var input = request.Input.Normalize();
        var exercise = await repository.FindExerciseForUpdateAsync(request.ExerciseId, cancellationToken)
            ?? throw new WritingResourceNotFoundException();
        if (exercise.Version != request.ExpectedVersion)
            throw new WritingConflictException("Bài tập đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        if (exercise.Part != WritingAdminMapping.ParsePart(input.Part))
            throw new WritingConflictException("Không thể đổi Part của bài tập đã tạo.");
        if (await repository.ExerciseCodeExistsAsync(input.Code, exercise.Id, cancellationToken))
            throw new WritingConflictException("Mã bài Writing đã tồn tại.");
        await CreateWritingExerciseCommandHandler.EnsureImageExistsAsync(input, repository, cancellationToken);
        exercise.Update(input.Code, input.Topic, WritingAdminMapping.ParseDifficulty(input.Difficulty),
            WritingAdminMapping.ParseExerciseStatus(input.Status), input.RubricNotes ?? string.Empty,
            input.ModelAnswer!, input.ImageAssetId, input.ImageAlt, input.KeywordOne, input.KeywordTwo,
            input.Sender, input.Recipient, input.Subject, input.EmailBody, input.Directions, input.Prompt,
            input.MinimumWordCount, input.Outline, request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return exercise.ToDto();
    }
}

public sealed class ArchiveWritingExerciseCommandHandler(IWritingAdminRepository repository, TimeProvider timeProvider)
    : IRequestHandler<ArchiveWritingExerciseCommand>
{
    public async Task Handle(ArchiveWritingExerciseCommand request, CancellationToken cancellationToken)
    {
        var exercise = await repository.FindExerciseForUpdateAsync(request.ExerciseId, cancellationToken)
            ?? throw new WritingResourceNotFoundException();
        if (exercise.Version != request.ExpectedVersion)
            throw new WritingConflictException("Bài tập đã được cập nhật bởi người khác. Hãy tải lại dữ liệu.");
        exercise.Archive(request.ActorUserId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }
}

internal static class WritingExerciseInputExtensions
{
    public static WritingExerciseInput Normalize(this WritingExerciseInput input) => input with
    {
        Code = input.Code.Trim(),
        Topic = input.Topic.Trim(),
        Difficulty = input.Difficulty.Trim().ToUpperInvariant(),
        Status = input.Status.Trim().ToUpperInvariant(),
        RubricNotes = TrimOrNull(input.RubricNotes),
        ModelAnswer = TrimOrNull(input.ModelAnswer),
        ImageAlt = TrimOrNull(input.ImageAlt),
        KeywordOne = TrimOrNull(input.KeywordOne),
        KeywordTwo = TrimOrNull(input.KeywordTwo),
        Sender = TrimOrNull(input.Sender),
        Recipient = TrimOrNull(input.Recipient),
        Subject = TrimOrNull(input.Subject),
        EmailBody = TrimOrNull(input.EmailBody),
        Directions = TrimOrNull(input.Directions),
        Prompt = TrimOrNull(input.Prompt),
        Outline = TrimOrNull(input.Outline)
    };

    public static WritingExercise ToEntity(this WritingExerciseInput input, Guid actorUserId, DateTimeOffset now) =>
        WritingExercise.Create(WritingAdminMapping.ParsePart(input.Part), input.Code, input.Topic,
            WritingAdminMapping.ParseDifficulty(input.Difficulty), WritingAdminMapping.ParseExerciseStatus(input.Status),
            input.RubricNotes ?? string.Empty, input.ModelAnswer!, input.ImageAssetId, input.ImageAlt,
            input.KeywordOne, input.KeywordTwo, input.Sender, input.Recipient, input.Subject, input.EmailBody,
            input.Directions, input.Prompt, input.MinimumWordCount, input.Outline, actorUserId, now);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
