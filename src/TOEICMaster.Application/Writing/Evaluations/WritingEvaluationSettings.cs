namespace TOEICMaster.Application.Writing.Evaluations;

/// <summary>Snapshot values stored with each job so later configuration changes do not rewrite its evaluation context.</summary>
public interface IWritingEvaluationSettings
{
    string Provider { get; }
    string Model { get; }
    string RubricVersion { get; }
}
