namespace TOEICMaster.Application.Writing.Admin;

public interface IWritingExerciseImportParser
{
    WritingExerciseImportPreviewDto Preview(int part, string fileName, byte[] content);
}

public sealed record WritingExerciseImportRowDto(int RowNumber, string? Code, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record WritingExerciseImportPreviewDto(int Part, string FileName,
    IReadOnlyList<WritingExerciseImportRowDto> Rows)
{
    public int ValidRowCount => Rows.Count(row => row.IsValid);
    public int InvalidRowCount => Rows.Count - ValidRowCount;
}

public sealed class InvalidWritingImportException(string message) : Exception(message);
