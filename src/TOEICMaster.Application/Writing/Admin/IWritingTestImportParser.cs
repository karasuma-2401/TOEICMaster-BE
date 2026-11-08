namespace TOEICMaster.Application.Writing.Admin;

public sealed record WritingTestImportPreviewDto(WritingTestInput? Test, IReadOnlyList<string> Errors)
{
    public bool IsValid => Test is not null && Errors.Count == 0;
}

public interface IWritingTestImportParser
{
    WritingTestImportPreviewDto Preview(byte[] content);
    byte[] CreateTemplate();
}
