using System.Globalization;
using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Admin;

/// <summary>Creates a complete draft test and its seven new exercises from one Excel file, or nothing at all.</summary>
public sealed record ImportSpeakingTestCommand(Guid ActorUserId, byte[] Workbook) : IRequest<SpeakingTestDetailDto>;

public sealed class ImportSpeakingTestCommandHandler(ISpeakingAdminRepository repository, ISpeakingMediaStore store,
    TimeProvider timeProvider) : IRequestHandler<ImportSpeakingTestCommand, SpeakingTestDetailDto>
{
    private static readonly SpeakingExerciseInputValidator ExerciseValidator = new();
    private static readonly SpeakingTestInputValidator TestValidator = new();

    public async Task<SpeakingTestDetailDto> Handle(ImportSpeakingTestCommand request,
        CancellationToken cancellationToken)
    {
        var workbook = SpeakingWorkbookReader.Read(request.Workbook);
        var failures = new List<ValidationFailure>();
        var testInput = await ValidateTestAsync(workbook, failures, cancellationToken);
        var rows = new List<ImportedRow>();
        foreach (var row in workbook.Rows)
            if (await ParseRowAsync(row, failures, cancellationToken) is { } parsed)
                rows.Add(parsed);
        ValidateStructure(workbook.Rows.Count, rows, failures);
        await ValidateUniquenessAsync(rows, failures, cancellationToken);
        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await SaveAsync(request.ActorUserId, testInput, rows, cancellationToken);
    }

    private static async Task<SpeakingTestInput> ValidateTestAsync(SpeakingImportWorkbook workbook,
        List<ValidationFailure> failures, CancellationToken cancellationToken)
    {
        // Slots are checked against the file's rows, so the test header is validated on its own here.
        var input = new SpeakingTestInput(workbook.Test.GetValueOrDefault("code"),
            workbook.Test.GetValueOrDefault("title"), workbook.Test.GetValueOrDefault("description"), []);
        var result = await TestValidator.ValidateAsync(input, cancellationToken);
        failures.AddRange(result.Errors.Select(failure =>
            new ValidationFailure($"Test.{failure.PropertyName}", failure.ErrorMessage)));
        return result.IsValid
            ? input with { Code = input.Code!.Trim(), Title = input.Title!.Trim(), Description = input.Description?.Trim() ?? "" }
            : input;
    }

    private static async Task<ImportedRow?> ParseRowAsync(SpeakingImportRow row, List<ValidationFailure> failures,
        CancellationToken cancellationToken)
    {
        void Fail(string field, string message) => failures.Add(new ValidationFailure($"Rows[{row.RowNumber}].{field}", message));

        if (!TryInteger(row.Cell("Part"), out var part) || !SpeakingTiming.IsPart(part))
        {
            Fail("Part", "Part phải từ 1 đến 5.");
            return null;
        }
        if (!TryInteger(row.Cell("Sequence"), out var sequence) || sequence < 1 ||
            sequence > SpeakingTiming.SlotsPerPart(part))
            Fail("Sequence", $"Part {part} chỉ có vị trí từ 1 đến {SpeakingTiming.SlotsPerPart(part)}.");

        var image = ReadImage(row, part, Fail);
        var input = ToInput(row, part, image);
        var validation = await ExerciseValidator.ValidateAsync(input, cancellationToken);
        foreach (var failure in validation.Errors)
            Fail(failure.PropertyName, failure.ErrorMessage);
        return validation.IsValid ? new ImportedRow(row.RowNumber, part, sequence, SpeakingExerciseInputs.Normalize(input), image) : null;
    }

    private static ImportedImage? ReadImage(SpeakingImportRow row, int part, Action<string, string> fail)
    {
        if (part != 2)
        {
            if (row.Images.Count > 0 || row.HasInCellImage)
                fail("Image", "Chỉ bài Part 2 mới có ảnh.");
            return null;
        }
        if (row.HasInCellImage)
        {
            fail("Image", "Ảnh đang ở dạng \"Place in Cell\". Hãy dùng Insert > Pictures > Place over Cells.");
            return null;
        }
        if (row.Images.Count > 1)
        {
            fail("Image", "Mỗi bài Part 2 chỉ được có một ảnh.");
            return null;
        }
        if (row.Images.Count == 0)
            return null;
        var bytes = row.Images[0];
        var contentType = bytes.Length <= SpeakingMediaRules.MaxImageBytes ? SpeakingMediaRules.DetectImage(bytes) : null;
        if (contentType is null)
        {
            fail("Image", "Ảnh phải là JPEG hoặc PNG hợp lệ, tối đa 5 MiB.");
            return null;
        }
        return new ImportedImage(bytes, contentType, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static SpeakingExerciseInput ToInput(SpeakingImportRow row, int part, ImportedImage? image)
    {
        var status = row.Cell("Status");
        var input = new SpeakingExerciseInput(part, row.Cell("Code"), row.Cell("Topic"), row.Cell("Difficulty"),
            string.IsNullOrWhiteSpace(status) ? SpeakingExerciseStatus.Draft.ToCode() : status, row.Cell("RubricNotes"),
            row.Cell("ModelAnswer"));
        string?[] Questions() => [row.Cell("Question1"), row.Cell("Question2"), row.Cell("Question3")];
        return part switch
        {
            1 => input with { Passage = row.Cell("Passage") },
            2 => input with { ImageAssetId = image is null ? null : Guid.NewGuid(), ImageAlt = row.Cell("ImageAlt") },
            3 => input with { Context = row.Cell("Context"), Questions = Questions() },
            4 => input with
            {
                DocumentTitle = row.Cell("DocumentTitle"),
                DocumentRows = ParseDocumentRows(row.Cell("DocumentRows")),
                CallerIntro = row.Cell("CallerIntro"),
                Questions = Questions()
            },
            _ => input with { Prompt = row.Cell("Prompt") }
        };
    }

    /// <summary>One "label | value" pair per line of the cell.</summary>
    private static SpeakingInfoRowInput?[] ParseDocumentRows(string text) => text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
        .Select(parts => new SpeakingInfoRowInput(parts[0], parts.Length == 2 ? parts[1] : null))
        .ToArray<SpeakingInfoRowInput?>();

    /// <summary>A file must describe the whole test, so every position has to be present exactly once.</summary>
    private static void ValidateStructure(int rowCount, List<ImportedRow> rows, List<ValidationFailure> failures)
    {
        var expected = Enumerable.Range(SpeakingTiming.MinPart, SpeakingTiming.MaxPart)
            .Sum(SpeakingTiming.SlotsPerPart);
        if (rowCount != expected)
            failures.Add(new ValidationFailure("Exercises", $"File cần đúng {expected} bài (Part 1–5: 2/2/1/1/1), hiện có {rowCount}."));
        foreach (var group in rows.GroupBy(row => (row.Part, row.Sequence)).Where(group => group.Count() > 1))
            failures.Add(new ValidationFailure("Exercises", $"Part {group.Key.Part} · bài {group.Key.Sequence} bị lặp ở các dòng {string.Join(", ", group.Select(row => row.RowNumber))}."));
        foreach (var group in rows.GroupBy(row => row.Input.Code!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            failures.Add(new ValidationFailure("Exercises", $"Mã bài {group.Key} bị lặp trong file."));
    }

    private async Task ValidateUniquenessAsync(List<ImportedRow> rows, List<ValidationFailure> failures,
        CancellationToken cancellationToken)
    {
        var bank = await repository.GetExerciseContentSourcesAsync(cancellationToken);
        foreach (var row in rows)
        {
            if (await repository.ExerciseCodeExistsAsync(row.Input.Code!, null, cancellationToken))
                failures.Add(new ValidationFailure($"Rows[{row.RowNumber}].Code", "Mã bài Speaking đã tồn tại."));
            failures.AddRange(SpeakingTestContentRules.FindBankDuplicates(row.Content, row.Image?.Sha256, bank)
                .Select(failure => new ValidationFailure($"Rows[{row.RowNumber}].{failure.Field}", failure.Message)));
        }

        failures.AddRange(SpeakingTestContentRules.FindDuplicatesInTest(rows.OrderBy(row => row.Part)
                .ThenBy(row => row.Sequence).Select(row => new SpeakingSlotContent(
                    $"Part {row.Part} · bài {row.Sequence}", row.Content, row.Image?.Sha256)))
            .Select(message => new ValidationFailure("Content", message)));
    }

    private async Task<SpeakingTestDetailDto> SaveAsync(Guid actorUserId, SpeakingTestInput testInput,
        IReadOnlyList<ImportedRow> rows, CancellationToken cancellationToken)
    {
        if (await repository.TestCodeExistsAsync(testInput.Code!, null, cancellationToken))
            throw new SpeakingConflictException("Mã đề Speaking đã tồn tại.");

        var now = timeProvider.GetUtcNow();
        var uploaded = new List<string>();
        try
        {
            var test = SpeakingTest.Create(testInput.Code!, testInput.Title!, testInput.Description!, actorUserId, now);
            var exercises = new Dictionary<Guid, SpeakingExercise>();
            var slots = new List<SpeakingTestExercise>();
            foreach (var row in rows.OrderBy(row => row.Part).ThenBy(row => row.Sequence))
            {
                var input = row.Input;
                if (row.Image is { } image)
                {
                    var objectKey = $"images/{Guid.NewGuid():N}.{SpeakingMediaRules.Extension(image.ContentType)}";
                    await store.UploadAsync(SpeakingMediaKind.Image, objectKey, image.Bytes, image.ContentType,
                        cancellationToken);
                    uploaded.Add(objectKey);
                    var asset = SpeakingMediaAsset.Create(SpeakingMediaKind.Image, objectKey, image.ContentType,
                        image.Bytes.Length, image.Sha256, actorUserId, now);
                    repository.AddMediaAsset(asset);
                    input = input with { ImageAssetId = asset.Id };
                }
                var exercise = SpeakingExercise.Create(input.Part, input.Code!, input.Topic!, input.ParseDifficulty(),
                    input.ParseStatus(), input.ToContentJson(), input.ImageAssetId, input.RubricNotes ?? string.Empty,
                    input.ModelAnswer!, actorUserId, now);
                repository.AddExercise(exercise);
                exercises[exercise.Id] = exercise;
                slots.Add(SpeakingTestExercise.Create(test.Id, row.Part, row.Sequence, exercise.Id));
            }
            repository.AddTest(test, slots);
            await repository.SaveChangesAsync(cancellationToken);
            return new SpeakingTestWithSlots(test, slots).ToDetailDto(exercises);
        }
        catch
        {
            foreach (var objectKey in uploaded)
            {
                try { await store.DeleteAsync(SpeakingMediaKind.Image, objectKey, CancellationToken.None); }
                catch { /* A rare orphan object is harmless; the database never references it. */ }
            }
            throw;
        }
    }

    private static bool TryInteger(string text, out int value)
    {
        value = 0;
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
            number != decimal.Truncate(number) || number is < int.MinValue or > int.MaxValue)
            return false;
        value = (int)number;
        return true;
    }

    private sealed record ImportedImage(byte[] Bytes, string ContentType, string Sha256);

    private sealed record ImportedRow(int RowNumber, int Part, int Sequence, SpeakingExerciseInput Input,
        ImportedImage? Image)
    {
        public SpeakingContentDto Content { get; } = SpeakingJson.Deserialize<SpeakingContentDto>(Input.ToContentJson());
    }
}
