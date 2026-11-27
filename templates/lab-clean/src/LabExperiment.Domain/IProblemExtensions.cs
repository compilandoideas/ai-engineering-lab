namespace LabExperiment.Domain;

/// <summary>
/// An error that carries data for the caller besides its code and message: the ProblemDetails
/// response publishes them as extensions.
/// </summary>
public interface IProblemExtensions
{
    IReadOnlyDictionary<string, object?> Extensions { get; }
}
