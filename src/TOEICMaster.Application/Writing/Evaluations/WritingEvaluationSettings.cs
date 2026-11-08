namespace TOEICMaster.Application.Writing.Evaluations;

/// <summary>Values snapshotted when a job is queued; a user-requested retry uses the current settings.</summary>
public interface IWritingEvaluationSettings
{
    string Provider { get; }
    string Model { get; }
    string RubricVersion { get; }
}
