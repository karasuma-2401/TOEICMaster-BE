namespace TOEICMaster.Application.ExerciseBank;

public interface ILrExerciseBankImportParser
{
    IReadOnlyList<LrExerciseItemInput> Parse(int part, string fileName, byte[] content);
}

public sealed class LrExerciseBankImportException(string message) : Exception(message);
public sealed class LrExerciseBankConflictException(string message) : Exception(message);
public sealed class InvalidLrExerciseBankItemException(string message) : Exception(message);
